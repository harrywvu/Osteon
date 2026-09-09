using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Mutes conflicting bindings, not action maps. Tracking, selection and UI clicks keep working,
/// and an XRI input mediator cannot undo the reservation just by enabling an action again.
/// </summary>
public sealed class AnatomyInputReservation : MonoBehaviour
{
    [SerializeField] private InputActionManager[] actionManagers;
    private readonly List<SavedBinding> saved = new List<SavedBinding>();
    private readonly List<InputActionAsset> cachedAssets = new List<InputActionAsset>();
    private readonly List<InputAction> selectionActions = new List<InputAction>();
    private readonly Dictionary<InputAction, bool> enabledStates = new Dictionary<InputAction, bool>();
    private bool initialized;
    private bool reserved;
    private bool changing;

    private struct SavedBinding
    {
        public InputAction action;
        public int index;
        public InputBinding binding;
    }

    public IEnumerable<InputActionAsset> Assets
    {
        get
        {
            Initialize();
            return cachedAssets;
        }
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (actionManagers == null) return;
        var seen = new HashSet<InputActionAsset>();
        foreach (var manager in actionManagers)
            if (manager != null && manager.actionAssets != null)
                foreach (var asset in manager.actionAssets)
                    if (asset != null && seen.Add(asset)) cachedAssets.Add(asset);
        foreach (var asset in cachedAssets)
            foreach (string hand in new[] { "Left", "Right" })
                foreach (string name in new[] { "Select", "UI Press" })
                {
                    var action = asset.FindAction($"XRI {hand} Interaction/{name}", false);
                    if (action != null) selectionActions.Add(action);
                }
    }

    public void Reserve()
    {
        if (reserved) return;
        reserved = true;
        // Capture before any binding cancellation can cause an input mediator to change states.
        foreach (var asset in Assets)
            foreach (var map in asset.actionMaps)
                foreach (var action in map.actions)
                    if (HasConflict(action)) enabledStates[action] = action.enabled;
        foreach (var asset in Assets)
            foreach (var map in asset.actionMaps)
                foreach (var action in map.actions)
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding binding = action.bindings[i];
                        if (!IsConflict(action, binding)) continue;
                        saved.Add(new SavedBinding { action = action, index = i, binding = binding });
                        action.ApplyBindingOverride(i, new InputBinding { overridePath = "" });
                    }
        InputSystem.onActionChange += OnActionChange;
    }

    private static bool IsConflict(InputAction action, InputBinding binding)
    {
        string path = binding.effectivePath ?? "";
        string map = action.actionMap?.name ?? "";
        bool right = path.IndexOf("RightHand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     (path.IndexOf("LeftHand", StringComparison.OrdinalIgnoreCase) < 0 &&
                      map.StartsWith("XRI Right", StringComparison.Ordinal));
        bool reservedControl = path.IndexOf("Primary2DAxis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("thumbstick", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("PrimaryButton", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.IndexOf("SecondaryButton", StringComparison.OrdinalIgnoreCase) >= 0;
        return right && reservedControl;
    }

    private static bool HasConflict(InputAction action)
    {
        foreach (var binding in action.bindings)
            if (IsConflict(action, binding)) return true;
        return false;
    }

    private void OnActionChange(object source, InputActionChange change)
    {
        if (changing || !reserved || change != InputActionChange.BoundControlsChanged) return;
        changing = true;
        try
        {
            foreach (var item in saved)
                if (item.action.bindings[item.index].overridePath != "")
                    item.action.ApplyBindingOverride(item.index, new InputBinding { overridePath = "" });
        }
        finally { changing = false; }
    }

    public void Release()
    {
        if (!reserved) return;
        reserved = false;
        InputSystem.onActionChange -= OnActionChange;
        foreach (var item in saved)
        {
            item.action.RemoveBindingOverride(item.index);
            if (item.binding.hasOverrides)
                item.action.ApplyBindingOverride(item.index, item.binding);
        }
        saved.Clear();
        foreach (var state in enabledStates)
        {
            if (state.Value) state.Key.Enable();
            else state.Key.Disable();
        }
        enabledStates.Clear();
    }

    public bool IsSelectionPressed()
    {
        Initialize();
        foreach (var action in selectionActions)
            if (action.IsPressed()) return true;
        return false;
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}

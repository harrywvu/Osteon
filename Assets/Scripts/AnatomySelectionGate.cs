/// <summary>Prevents a held selection or two callbacks in one frame from skipping views.</summary>
public sealed class AnatomySelectionGate
{
    private int blockedFrame = -1;
    public bool CanSelect { get; private set; } = true;

    public void Block(int frame)
    {
        blockedFrame = frame;
        CanSelect = false;
    }

    public void Poll(bool selectionPressed, int frame)
    {
        if (!selectionPressed && frame > blockedFrame)
            CanSelect = true;
    }
}

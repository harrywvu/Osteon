package com.osteon.tts;

import android.content.Context;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;
import java.util.Locale;
import java.util.Set;

/** Uses only an installed English voice that does not require a network. */
public final class OfflineSpeech implements TextToSpeech.OnInitListener {
    private final TextToSpeech tts;
    private volatile int initialization; // 0 pending, 1 initialized, -1 failed
    private volatile int playbackState; // 0 queued, 1 speaking, 2 done, -1 failed
    private volatile String activeId;
    private int serial;
    private boolean voiceChecked;

    public OfflineSpeech(Context context) {
        tts = new TextToSpeech(context, this);
        tts.setOnUtteranceProgressListener(new UtteranceProgressListener() {
            @Override public void onStart(String id) {
                if (id.equals(activeId)) playbackState = 1;
            }

            @Override public void onDone(String id) {
                if (id.equals(activeId)) playbackState = 2;
            }

            @Override public void onError(String id) {
                if (id.equals(activeId)) playbackState = -1;
            }

            @Override public void onError(String id, int errorCode) {
                if (id.equals(activeId)) playbackState = -1;
            }
        });
    }

    @Override public void onInit(int status) {
        initialization = status == TextToSpeech.SUCCESS ? 1 : -1;
    }

    public int getState() {
        if (initialization <= 0) return initialization;
        if (!voiceChecked) {
            voiceChecked = true;
            Voice best = null;
            Set<Voice> voices = tts.getVoices();
            if (voices != null) {
                for (Voice voice : voices) {
                    Locale locale = voice.getLocale();
                    Set<String> features = voice.getFeatures();
                    if (locale == null || !"en".equals(locale.getLanguage()) ||
                        voice.isNetworkConnectionRequired() ||
                        (features != null && features.contains(
                            TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED))) continue;
                    if (best == null || voice.getQuality() > best.getQuality()) best = voice;
                }
            }
            if (best == null || tts.setVoice(best) != TextToSpeech.SUCCESS)
                initialization = -1;
        }
        return initialization;
    }

    public boolean speak(String text) {
        if (getState() != 1 || text == null || text.isEmpty() ||
            text.length() > TextToSpeech.getMaxSpeechInputLength()) return false;
        String id = "osteon-" + (++serial);
        activeId = id;
        playbackState = 0;
        if (tts.speak(text, TextToSpeech.QUEUE_FLUSH, null, id) == TextToSpeech.SUCCESS)
            return true;
        activeId = null;
        playbackState = -1;
        return false;
    }

    public int getPlaybackState() { return playbackState; }

    public void stop() {
        activeId = null;
        tts.stop();
        playbackState = 0;
    }

    public void shutdown() {
        stop();
        tts.shutdown();
    }
}

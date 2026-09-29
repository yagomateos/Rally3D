// Co-driver voice for the web build, using the browser's own speech synthesis (no audio files).
// Picks a Spanish voice when the browser has one; otherwise the browser's default voice reads the text.
mergeInto(LibraryManager.library, {
  RallySpeech_Speak: function (textPtr, rate) {
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;
    var text = UTF8ToString(textPtr);
    var synth = window.speechSynthesis;
    if (!window.__rallyVoice) {
      var voices = synth.getVoices() || [];
      var es = voices.filter(function (v) { return /^es(-|_|$)/i.test(v.lang); });
      window.__rallyVoice = es.find(function (v) { return /es-ES/i.test(v.lang); }) || es[0] || null;
    }
    var u = new SpeechSynthesisUtterance(text);
    u.lang = 'es-ES';
    if (window.__rallyVoice) u.voice = window.__rallyVoice;
    u.rate = rate;
    u.volume = 1;
    synth.cancel(); // never queue up stale calls behind the current one
    synth.speak(u);
  },

  RallySpeech_Stop: function () {
    if (typeof window !== 'undefined' && 'speechSynthesis' in window) window.speechSynthesis.cancel();
  }
});

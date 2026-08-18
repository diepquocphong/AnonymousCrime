(function () {
  "use strict";

  var STORAGE_KEY = "Franklin.Menu.Audio.Enabled.v1";
  var MASTER_STORAGE_KEY = "Franklin.Menu.Audio.Master.v2";
  var MUSIC_STORAGE_KEY = "Franklin.Menu.Audio.Music.v2";
  var SFX_STORAGE_KEY = "Franklin.Menu.Audio.Sfx.v2";
  var MUSIC_BASE_VOLUME = 0.32;
  var music = null;
  var voices = Object.create(null);
  var lastPlayedAt = Object.create(null);
  var lastEffectAt = 0;
  var lastEffectName = "";
  var hoverIndex = Math.floor(Math.random() * 3);
  var enabled = readStoredEnabled();
  var masterLevel = readStoredPercentage(MASTER_STORAGE_KEY, 100);
  var musicLevel = readStoredPercentage(MUSIC_STORAGE_KEY, 70);
  var sfxLevel = readStoredPercentage(SFX_STORAGE_KEY, 100);
  var unlocked = false;
  var hostSuspended = false;
  var pageHidden = document.hidden;
  var windowBlurred = false;
  var portraitSuspended = window.innerHeight > window.innerWidth;
  var ducked = false;
  var suspended = pageHidden || portraitSuspended;

  var sounds = {
    hover: {
      sources: [
        "assets/audio/ui-hover-01.wav",
        "assets/audio/ui-hover-02.wav",
        "assets/audio/ui-hover-03.wav"
      ],
      volume: 0.24,
      cooldown: 75
    },
    activate: {
      sources: ["assets/audio/ui-activate.wav"],
      volume: 0.66,
      cooldown: 120
    },
    back: {
      sources: ["assets/audio/ui-back.wav"],
      volume: 0.38,
      cooldown: 120
    },
    panel: {
      sources: ["assets/audio/ui-panel.wav"],
      volume: 0.26,
      cooldown: 140
    },
    transition: {
      sources: ["assets/audio/ui-transition.wav"],
      volume: 0.46,
      cooldown: 450
    },
    denied: {
      sources: ["assets/audio/ui-denied.wav"],
      volume: 0.42,
      cooldown: 300
    },
    error: {
      sources: ["assets/audio/ui-error.wav"],
      volume: 0.42,
      cooldown: 500
    },
    notify: {
      sources: ["assets/audio/ui-notify.wav"],
      volume: 0.44,
      cooldown: 700
    }
  };

  function readStoredEnabled() {
    try {
      var value = window.localStorage.getItem(STORAGE_KEY);
      return value === null ? true : value !== "0";
    } catch (error) {
      return true;
    }
  }

  function storeEnabled(nextEnabled) {
    try {
      window.localStorage.setItem(STORAGE_KEY, nextEnabled ? "1" : "0");
    } catch (error) {
      // Unity PlayerPrefs remains authoritative in the embedded WebView.
    }
  }

  function clampPercentage(value, fallback) {
    var number = Number(value);
    if (!Number.isFinite(number)) return fallback;
    return Math.max(0, Math.min(100, Math.round(number)));
  }

  function readStoredPercentage(key, fallback) {
    try {
      var value = window.localStorage.getItem(key);
      return value === null ? fallback : clampPercentage(value, fallback);
    } catch (error) {
      return fallback;
    }
  }

  function storePercentage(key, value) {
    try {
      window.localStorage.setItem(key, String(value));
    } catch (error) {
      // Unity PlayerPrefs remains authoritative in the embedded WebView.
    }
  }

  function createAudio(source, preload) {
    var audio = new Audio();
    audio.preload = preload;
    audio.src = source;
    audio.setAttribute("playsinline", "");
    audio.setAttribute("webkit-playsinline", "");
    return audio;
  }

  function prepareAudio() {
    if (!music) {
      music = createAudio("assets/audio/menu-ambient.m4a", "metadata");
      music.loop = true;
      music.volume = 0;
    }

    Object.keys(sounds).forEach(function (name) {
      sounds[name].sources.forEach(function (source) {
        if (!voices[source]) {
          voices[source] = createAudio(source, "auto");
        }
      });
    });
  }

  function safePlay(audio) {
    if (!audio) return;
    try {
      var result = audio.play();
      if (result && typeof result.catch === "function") {
        result.catch(function () {});
      }
    } catch (error) {
      // Autoplay can be rejected until the first trusted pointer/key gesture.
    }
  }

  function stopEffects() {
    Object.keys(voices).forEach(function (source) {
      var voice = voices[source];
      try {
        voice.pause();
        voice.currentTime = 0;
      } catch (error) {
        // A voice that has not loaded yet has nothing to stop.
      }
    });
  }

  function refreshMusicVolume() {
    if (!music) return;
    var volume = MUSIC_BASE_VOLUME * (masterLevel / 100) * (musicLevel / 100);
    music.volume = Math.max(0, Math.min(1, volume * (ducked ? 0.5 : 1)));
  }

  function syncMusicPlayback() {
    prepareAudio();
    if (!enabled || !unlocked || suspended || masterLevel === 0 || musicLevel === 0) {
      if (music && !music.paused) music.pause();
      return;
    }
    refreshMusicVolume();
    safePlay(music);
  }

  function refreshSuspension() {
    var nextSuspended = hostSuspended || pageHidden || windowBlurred || portraitSuspended;
    if (nextSuspended === suspended) return;
    suspended = nextSuspended;
    if (suspended) {
      if (music) music.pause();
      stopEffects();
    } else {
      syncMusicPlayback();
    }
  }

  function unlock(event) {
    prepareAudio();
    unlocked = true;
    var toggle = document.getElementById("audio-toggle");
    if (enabled && event && toggle &&
        (event.target === toggle || toggle.contains(event.target))) {
      return;
    }
    var actionTarget = event ? event.target : null;
    while (actionTarget && actionTarget !== document) {
      if (actionTarget.getAttribute) {
        var action = actionTarget.getAttribute("data-action");
        if (action === "continue" || action === "quit") {
          return;
        }
      }
      actionTarget = actionTarget.parentNode;
    }
    syncMusicPlayback();
  }

  function chooseSource(name, definition) {
    if (name !== "hover" || definition.sources.length < 2) {
      return definition.sources[0];
    }
    hoverIndex = (hoverIndex + 1) % definition.sources.length;
    return definition.sources[hoverIndex];
  }

  function play(name) {
    var definition = sounds[name];
    if (!definition || !enabled || !unlocked || suspended ||
        masterLevel === 0 || sfxLevel === 0) return false;
    var now = window.performance && typeof window.performance.now === "function"
      ? window.performance.now()
      : Date.now();
    if (lastPlayedAt[name] && now - lastPlayedAt[name] < definition.cooldown) {
      return false;
    }
    if (now - lastEffectAt < 70) {
      if (name === "hover") return false;
      if (lastEffectName !== "hover" && name !== "error" && name !== "transition") {
        return false;
      }
    }
    lastPlayedAt[name] = now;
    lastEffectAt = now;
    lastEffectName = name;

    prepareAudio();
    var source = chooseSource(name, definition);
    var voice = voices[source];
    try {
      voice.pause();
      voice.currentTime = 0;
      voice.volume = Math.max(
        0,
        Math.min(1, definition.volume * (masterLevel / 100) * (sfxLevel / 100))
      );
      safePlay(voice);
    } catch (error) {
      return false;
    }
    return true;
  }

  function setEnabled(nextEnabled, persist) {
    enabled = Boolean(nextEnabled);
    if (persist !== false) storeEnabled(enabled);
    if (!enabled) {
      if (music) music.pause();
      stopEffects();
    } else {
      syncMusicPlayback();
    }
    return enabled;
  }

  function setDucked(nextDucked) {
    ducked = Boolean(nextDucked);
    refreshMusicVolume();
  }

  function setMix(nextMix, persist) {
    nextMix = nextMix || {};
    masterLevel = clampPercentage(nextMix.master, masterLevel);
    musicLevel = clampPercentage(nextMix.music, musicLevel);
    sfxLevel = clampPercentage(nextMix.sfx, sfxLevel);
    if (persist !== false) {
      storePercentage(MASTER_STORAGE_KEY, masterLevel);
      storePercentage(MUSIC_STORAGE_KEY, musicLevel);
      storePercentage(SFX_STORAGE_KEY, sfxLevel);
    }
    if (masterLevel === 0 || sfxLevel === 0) stopEffects();
    refreshMusicVolume();
    syncMusicPlayback();
    return { master: masterLevel, music: musicLevel, sfx: sfxLevel };
  }

  function setHostSuspended(nextSuspended) {
    hostSuspended = Boolean(nextSuspended);
    if (!hostSuspended) windowBlurred = false;
    refreshSuspension();
  }

  function handleViewportChange() {
    portraitSuspended = window.innerHeight > window.innerWidth;
    refreshSuspension();
  }

  prepareAudio();

  if (window.PointerEvent) {
    document.addEventListener("pointerdown", unlock, { capture: true, passive: true });
  } else {
    document.addEventListener("touchstart", unlock, { capture: true, passive: true });
    document.addEventListener("mousedown", unlock, { capture: true, passive: true });
  }
  document.addEventListener("keydown", unlock, { capture: true });
  document.addEventListener("visibilitychange", function () {
    pageHidden = document.hidden;
    refreshSuspension();
  });
  window.addEventListener("pagehide", function () {
    pageHidden = true;
    refreshSuspension();
  });
  window.addEventListener("pageshow", function () {
    pageHidden = document.hidden;
    refreshSuspension();
  });
  window.addEventListener("blur", function () {
    windowBlurred = true;
    refreshSuspension();
  });
  window.addEventListener("focus", function () {
    windowBlurred = false;
    refreshSuspension();
  });
  window.addEventListener("resize", handleViewportChange, { passive: true });
  window.addEventListener("orientationchange", handleViewportChange, { passive: true });

  window.FranklinMenuAudio = Object.freeze({
    unlock: unlock,
    play: play,
    setEnabled: setEnabled,
    isEnabled: function () { return enabled; },
    setMix: setMix,
    getMix: function () {
      return { master: masterLevel, music: musicLevel, sfx: sfxLevel };
    },
    setDucked: setDucked,
    setSuspended: setHostSuspended,
    getDebugState: function () {
      return {
        enabled: enabled,
        unlocked: unlocked,
        suspended: suspended,
        ducked: ducked,
        master: masterLevel,
        music: musicLevel,
        sfx: sfxLevel,
        musicPaused: music ? music.paused : true,
        musicTime: music ? music.currentTime : 0,
        voiceCount: Object.keys(voices).length
      };
    }
  });
}());

(function () {
  "use strict";

  var DESIGN_WIDTH = 1920;
  var DESIGN_HEIGHT = 1080;
  var MESSAGE_VERSION = 1;
  var stage = document.getElementById("design-stage");
  var safeProbe = document.getElementById("safe-area-probe");
  var toast = document.getElementById("menu-toast");
  var busyOverlay = document.getElementById("menu-busy");
  var busyLabel = document.getElementById("menu-busy-label");
  var versionLabel = document.getElementById("game-version");
  var syncLabel = document.getElementById("sync-state");
  var audioToggle = document.getElementById("audio-toggle");
  var audioStateLabel = document.getElementById("audio-state");
  var continueButton = document.querySelector('[data-action="continue"]');
  var newGameButton = document.querySelector('[data-action="new-game"]');
  var missionMenuButton = document.querySelector('[data-action="mission-open"]');
  var quitButton = document.querySelector('[data-action="quit"]');
  var profileMenuButton = document.querySelector('[data-action="profile"]');
  var actionButtons = Array.prototype.slice.call(document.querySelectorAll("[data-action]"));
  var settingsMenuButton = document.querySelector('[data-action="settings"]');
  var settingsScreen = document.getElementById("settings-screen");
  var settingsPanel = settingsScreen
    ? settingsScreen.querySelector(".settings-panel")
    : null;
  var settingsCloseButtons = settingsScreen
    ? Array.prototype.slice.call(settingsScreen.querySelectorAll("[data-settings-close]"))
    : [];
  var settingsControls = settingsScreen
    ? Array.prototype.slice.call(settingsScreen.querySelectorAll("[data-setting]"))
    : [];
  var settingsTabs = settingsScreen
    ? Array.prototype.slice.call(settingsScreen.querySelectorAll("[data-settings-tab]"))
    : [];
  var settingsPanes = settingsScreen
    ? Array.prototype.slice.call(settingsScreen.querySelectorAll("[data-settings-pane]"))
    : [];
  var settingsAudioMaster = settingsScreen
    ? settingsScreen.querySelector("[data-audio-enabled]")
    : null;
  var settingsAudioRanges = settingsScreen
    ? Array.prototype.slice.call(settingsScreen.querySelectorAll("[data-audio-range]"))
    : [];
  var settingsLanguageButtons = settingsScreen
    ? Array.prototype.slice.call(settingsScreen.querySelectorAll("[data-language]"))
    : [];
  var qualitySummary = document.getElementById("quality-summary");
  var missionScreen = document.getElementById("mission-screen");
  var missionPanel = missionScreen
    ? missionScreen.querySelector(".mission-panel")
    : null;
  var missionCards = missionScreen
    ? Array.prototype.slice.call(missionScreen.querySelectorAll("[data-mission-card]"))
    : [];
  var missionGrid = missionScreen
    ? missionScreen.querySelector(".mission-grid")
    : null;
  var missionBackButton = missionScreen
    ? missionScreen.querySelector("[data-mission-back]")
    : null;
  var missionPlayButton = missionScreen
    ? missionScreen.querySelector("[data-mission-play]")
    : null;
  var missionProgress = document.getElementById("mission-progress");
  var missionProgressValue = document.getElementById("mission-progress-value");
  var missionDetailNumber = document.getElementById("mission-detail-number");
  var missionDetailSender = document.getElementById("mission-detail-sender");
  var missionDetailTitle = document.getElementById("mission-detail-title");
  var missionDetailDescription = document.getElementById("mission-detail-description");
  var missionDetailState = document.getElementById("mission-detail-state");
  var missionHelp = document.getElementById("mission-help");
  var onlineMenuButton = document.querySelector('[data-action="online-open"]');
  var onlineScreen = document.getElementById("online-screen");
  var onlinePanel = onlineScreen ? onlineScreen.querySelector(".online-panel") : null;
  var onlineBackButton = onlineScreen ? onlineScreen.querySelector("[data-online-back]") : null;
  var onlineRetryButton = onlineScreen ? onlineScreen.querySelector("[data-online-retry]") : null;
  var onlineBackLabel = document.getElementById("online-back-label");
  var onlineStatus = document.getElementById("online-status");
  var onlineStatusDetail = document.getElementById("online-status-detail");
  var onlineProgress = onlineScreen ? onlineScreen.querySelector(".online-search-progress") : null;
  var onlineProgressBar = document.getElementById("online-progress-bar");
  var onlineTime = document.getElementById("online-time");
  var onlinePlayerCount = document.getElementById("online-player-count");
  var onlinePlayerSlots = onlineScreen
    ? Array.prototype.slice.call(onlineScreen.querySelectorAll("[data-online-slot]"))
    : [];
  var guestScreen = document.getElementById("guest-screen");
  var guestPanel = guestScreen ? guestScreen.querySelector(".guest-panel") : null;
  var guestBackButton = guestScreen ? guestScreen.querySelector("[data-guest-back]") : null;
  var guestModeButtons = guestScreen
    ? Array.prototype.slice.call(guestScreen.querySelectorAll("[data-guest-mode]"))
    : [];
  var guestAvatarButtons = guestScreen
    ? Array.prototype.slice.call(guestScreen.querySelectorAll("[data-guest-avatar]"))
    : [];
  var guestCreateButton = guestScreen
    ? guestScreen.querySelector("[data-guest-create]")
    : null;
  var guestLoginButton = guestScreen
    ? guestScreen.querySelector("[data-guest-login]")
    : null;
  var guestNameInput = document.getElementById("guest-name-input");
  var guestCreatePane = document.getElementById("guest-create-pane");
  var guestLoginPane = document.getElementById("guest-login-pane");
  var guestFeedback = document.getElementById("guest-feedback");
  var guestProfileId = document.getElementById("guest-profile-id");
  var guestProfileName = document.getElementById("guest-profile-name");
  var guestLastPlayed = document.getElementById("guest-last-played");
  var guestCreateLabel = document.getElementById("guest-create-label");
  var layoutFrame = 0;
  var toastTimer = 0;
  var settingsFocusBeforeOpen = null;
  var missionFocusBeforeOpen = null;
  var missionPlayPending = false;
  var missionPlayTimer = 0;
  var missionHelpTimer = 0;
  var missionAutoScrollTimer = 0;
  var missionScrollTouched = false;
  var missionLastCenteredId = "";
  var missionSwipeGesture = false;
  var missionSwipeClickUntil = 0;
  var missionPointerStartX = 0;
  var missionPointerStartY = 0;
  var missionPointerStartScrollLeft = 0;
  var missionPointerTracking = false;
  var onlineFocusBeforeOpen = null;
  var onlineMode = "closed";
  var onlineSearchInterval = 0;
  var onlineSearchTimeout = 0;
  var onlineSearchRunId = 0;
  var onlineSearchStartedAt = 0;
  var onlineSearchDeadline = 0;
  var onlineSearchDuration = 0;
  var onlineLastRenderedSecond = -1;
  var onlinePlayers = [];
  var guestFocusBeforeOpen = null;
  var guestMode = "create";
  var guestDraftAvatar = "avatar-1";
  var guestRequestPending = "";
  var guestPendingRequestId = "";
  var guestRequestSequence = 0;
  var guestRequestTimer = 0;
  var guestKeyboardScale = 0;
  var guestKeyboardViewportWidth = 0;
  var currentLayoutScale = 1;
  var menuBusy = false;
  var userInteracted = false;
  var lastPointerInputAt = 0;
  var audioToggleReadyAt = 0;
  var settingsView = "graphics";
  var i18n = window.FranklinMenuI18n || {
    apply: function () { return "vi"; },
    t: function (key, fallback) { return fallback || key; },
    normalize: function () { return "vi"; },
    localizeMission: function (item) { return item; }
  };

  function localizeMessageToken(token, fallbackKey, fallbackText) {
    if (typeof token === "string" && token.trim()) {
      return i18n.t(token.trim(), token.trim());
    }
    return i18n.t(fallbackKey, fallbackText);
  }
  var menuAudio = window.FranklinMenuAudio || {
    unlock: function () {},
    play: function () { return false; },
    setEnabled: function () { return false; },
    isEnabled: function () { return true; },
    setMix: function (mix) { return mix || { master: 100, music: 70, sfx: 100 }; },
    getMix: function () { return { master: 100, music: 70, sfx: 100 }; },
    setDucked: function () {},
    setSuspended: function () {}
  };
  var isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) ||
    (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
  var hostMetrics = {
    safeLeft: 0,
    safeRight: 0,
    safeTop: 0,
    safeBottom: 0
  };

  var state = {
    version: "v0.1.0",
    hasSave: false,
    sync: "offline",
    profile: "guest",
    guest: {
      exists: false,
      active: false,
      id: "",
      label: "GUEST-LOCAL",
      displayName: "",
      avatar: "avatar-1",
      createdUtc: "",
      lastPlayedUtc: "",
      progressOnDevice: true,
      response: "idle",
      responseId: "",
      message: ""
    },
    showQuit: !isIOS,
    audioEnabled: menuAudio.isEnabled(),
    audio: {
      enabled: menuAudio.isEnabled(),
      master: menuAudio.getMix().master,
      music: menuAudio.getMix().music,
      sfx: menuAudio.getMix().sfx
    },
    language: "vi",
    graphics: {
      quality: "auto",
      antiAliasing: "fxaa",
      depthOfField: "off",
      fbsEnabled: true,
      ssaoEnabled: false,
      autoProfile: "balanced",
      ssaoAvailable: true,
      dofAvailable: true,
      canChangeFbs: true,
      canChangeSsao: true,
      canChangeAntiAliasing: true,
      canChangeDepthOfField: true
    },
    missions: {
      activeId: "",
      selectedId: "strawberry-pickup",
      completedCount: 0,
      totalCount: 6,
      items: [
        {
          id: "strawberry-pickup",
          number: 1,
          title: "CUỘC HẸN STRAWBERRY",
          sender: "LAMAR",
          description: "Đến Strawberry và tìm một chiếc xe đủ nhanh cho chuyến lấy hàng. Giữ phương tiện nguyên vẹn khi tới điểm hẹn.",
          thumbnail: "assets/missions/mission-01-strawberry-pickup.webp",
          unlocked: true,
          completed: false
        },
        {
          id: "warehouse-recon",
          number: 2,
          title: "TRINH SÁT KHO HÀNG",
          sender: "LESTER",
          description: "Tiếp cận nhà kho được đánh dấu, quan sát kín đáo và chụp ảnh chiếc xe van giao hàng mà không gây chú ý.",
          thumbnail: "assets/missions/mission-02-warehouse-recon.webp",
          unlocked: false,
          completed: false
        },
        {
          id: "downtown-vip",
          number: 3,
          title: "VIP DOWNTOWN",
          sender: "DOWNTOWN CAB",
          description: "Một khách VIP đang chờ giữa trung tâm. Đến điểm đón trước khi hết thời gian và hoàn thành chuyến xe an toàn.",
          thumbnail: "assets/missions/mission-03-downtown-vip.webp",
          unlocked: false,
          completed: false
        },
        {
          id: "range-pressure",
          number: 4,
          title: "ÁP LỰC TRƯỜNG BẮN",
          sender: "RANGE CONTROL",
          description: "Đến trường bắn phía tây và hạ đủ mục tiêu trong ba lượt. Giữ độ chính xác trước khi bộ đếm thời gian kết thúc.",
          thumbnail: "assets/missions/mission-04-range-pressure.webp",
          unlocked: false,
          completed: false
        },
        {
          id: "fuel-run",
          number: 5,
          title: "CHUYẾN HÀNG NÓNG",
          sender: "LAMAR",
          description: "Nhận chiếc mô tô thể thao tại trạm xăng và đưa nó tới garage trước khi hết giờ. Tránh va chạm để giữ nguyên giá trị lô hàng.",
          thumbnail: "assets/missions/mission-05-fuel-run.webp",
          unlocked: false,
          completed: false
        },
        {
          id: "carrier-signal",
          number: 6,
          title: "MẬT LỆNH NGOÀI KHƠI",
          sender: "CONTROL",
          description: "Dùng drone trinh sát boong tàu sân bay, đánh dấu tín hiệu rồi rời khu vực bằng Gyrocopter mà không gây báo động.",
          thumbnail: "assets/missions/mission-06-carrier-signal.webp",
          unlocked: false,
          completed: false
        }
      ]
    }
  };

  var actionLabels = {
    "continue": "Tiếp tục",
    "new-game": "Trò chơi mới",
    "mission-open": "Nhiệm vụ",
    "online-open": "Chơi online",
    "settings": "Cài đặt",
    "quit": "Thoát",
    "profile": "Hồ sơ",
    "sync": "Đồng bộ",
    "audio-toggle": "Âm thanh",
    "settings-quality": "Chất lượng",
    "settings-aa": "Khử răng cưa",
    "settings-dof": "Lấy nét",
    "settings-fbs": "Bóng nhân vật",
    "settings-ssao": "Đổ bóng môi trường",
    "settings-audio-enabled": "Âm thanh menu",
    "settings-audio-master": "Âm lượng tổng",
    "settings-audio-music": "Nhạc nền menu",
    "settings-audio-sfx": "Hiệu ứng giao diện",
    "settings-language": "Ngôn ngữ",
    "settings-complete": "Cài đặt",
    "mission-select": "Chọn màn chơi",
    "mission-play": "Bắt đầu màn chơi",
    "guest-create": "Tạo hồ sơ khách",
    "guest-login": "Đăng nhập hồ sơ khách"
  };

  function readPixel(style, property) {
    var value = parseFloat(style[property]);
    return Number.isFinite(value) ? value : 0;
  }

  function readCssNumber(style, property, fallback) {
    var value = parseFloat(style.getPropertyValue(property));
    return Number.isFinite(value) ? value : fallback;
  }

  function setBoundedType(root, rootStyle, property, minimumProperty, maximumProperty, preferred) {
    var minimum = readCssNumber(rootStyle, minimumProperty, preferred);
    var maximum = readCssNumber(rootStyle, maximumProperty, preferred);
    var bounded = Math.max(minimum, Math.min(maximum, preferred));
    root.style.setProperty(property, bounded.toFixed(2) + "px");
  }

  function updateBoundedTypography(scale) {
    var root = document.documentElement;
    var rootStyle = window.getComputedStyle(root);
    var compactProgress = Math.max(0, Math.min(1, (0.42 - scale) / 0.19));

    setBoundedType(root, rootStyle, "--menu-label-size", "--menu-label-size-min", "--menu-label-size-max", 31 + 5 * compactProgress);
    setBoundedType(root, rootStyle, "--top-action-label-size", "--top-action-label-size-min", "--top-action-label-size-max", 29 + 4 * compactProgress);
    setBoundedType(root, rootStyle, "--top-sync-label-size", "--top-sync-label-size-min", "--top-sync-label-size-max", 25 + 4 * compactProgress);
    setBoundedType(root, rootStyle, "--mission-heading-size", "--mission-heading-size-min", "--mission-heading-size-max", 60 + 4 * compactProgress);
    setBoundedType(root, rootStyle, "--mission-number-size", "--mission-number-size-min", "--mission-number-size-max", 21 + 5 * compactProgress);
    setBoundedType(root, rootStyle, "--mission-card-title-size", "--mission-card-title-size-min", "--mission-card-title-size-max", 21 + 7 * compactProgress);
    setBoundedType(root, rootStyle, "--mission-card-status-size", "--mission-card-status-size-min", "--mission-card-status-size-max", 15 + 5 * compactProgress);
    setBoundedType(root, rootStyle, "--mission-lock-size", "--mission-lock-size-min", "--mission-lock-size-max", Math.max(112, 44 / Math.max(scale, 0.001)));
  }

  function updateLayout() {
    layoutFrame = 0;
    var viewport = window.visualViewport;
    var viewportWidth = viewport ? viewport.width : window.innerWidth;
    var viewportHeight = viewport ? viewport.height : window.innerHeight;
    var viewportLeft = viewport ? viewport.offsetLeft : 0;
    var viewportTop = viewport ? viewport.offsetTop : 0;
    var safeStyle = window.getComputedStyle(safeProbe);
    var safeTop = Math.max(
      readPixel(safeStyle, "paddingTop"),
      hostMetrics.safeTop * viewportHeight
    );
    var safeRight = Math.max(
      readPixel(safeStyle, "paddingRight"),
      hostMetrics.safeRight * viewportWidth
    );
    var safeBottom = Math.max(
      readPixel(safeStyle, "paddingBottom"),
      hostMetrics.safeBottom * viewportHeight
    );
    var safeLeft = Math.max(
      readPixel(safeStyle, "paddingLeft"),
      hostMetrics.safeLeft * viewportWidth
    );
    var usableWidth = Math.max(1, viewportWidth - safeLeft - safeRight);
    var usableHeight = Math.max(1, viewportHeight - safeTop - safeBottom);
    var naturalScale = Math.min(usableWidth / DESIGN_WIDTH, usableHeight / DESIGN_HEIGHT);
    var keyboardConstrained = Boolean(
      guestKeyboardScale > 0 &&
      isGuestOpen() &&
      Math.abs(viewportWidth - guestKeyboardViewportWidth) <=
        Math.max(8, guestKeyboardViewportWidth * 0.08) &&
      naturalScale < guestKeyboardScale * 0.82
    );
    var scale = keyboardConstrained ? guestKeyboardScale : naturalScale;
    var minimumHitInDesignPixels = 48 / Math.max(scale, 0.001);
    var hitHeight = Math.min(230, Math.max(116, minimumHitInDesignPixels));
    currentLayoutScale = scale;

    document.documentElement.style.setProperty("--ui-scale", scale.toFixed(6));
    document.documentElement.style.setProperty("--menu-hit-height", hitHeight.toFixed(2) + "px");
    document.documentElement.style.setProperty("--top-hit-height", hitHeight.toFixed(2) + "px");
    document.documentElement.style.setProperty(
      "--stage-left",
      (viewportLeft + safeLeft + usableWidth * 0.5).toFixed(2) + "px"
    );
    document.documentElement.style.setProperty(
      "--stage-top",
      (viewportTop + safeTop + usableHeight * 0.5).toFixed(2) + "px"
    );
    document.documentElement.classList.toggle("is-compact-viewport", scale < 0.42);
    document.documentElement.classList.toggle("is-ultra-compact-viewport", scale < 0.28);
    document.documentElement.classList.toggle("is-micro-viewport", scale < 0.23);
    document.documentElement.classList.toggle("is-guest-keyboard-open", keyboardConstrained);
    updateBoundedTypography(scale);
  }

  function requestLayout() {
    if (layoutFrame) return;
    layoutFrame = window.requestAnimationFrame(updateLayout);
  }

  function normalizeState(nextState) {
    if (typeof nextState === "string") {
      try {
        nextState = JSON.parse(nextState);
      } catch (error) {
        return null;
      }
    }

    return nextState && typeof nextState === "object" ? nextState : null;
  }

  function isOneOf(value, values) {
    return typeof value === "string" && values.indexOf(value.toLowerCase()) >= 0;
  }

  function mergeGraphicsState(nextGraphics) {
    if (!nextGraphics || typeof nextGraphics !== "object") return;
    var graphics = state.graphics;
    if (isOneOf(nextGraphics.quality, ["auto", "low", "balanced", "high"])) {
      graphics.quality = nextGraphics.quality.toLowerCase();
    }
    if (isOneOf(nextGraphics.antiAliasing, ["off", "fxaa", "smaa"])) {
      graphics.antiAliasing = nextGraphics.antiAliasing.toLowerCase();
    }
    if (isOneOf(nextGraphics.depthOfField, ["off", "near", "far"])) {
      graphics.depthOfField = nextGraphics.depthOfField.toLowerCase();
    }
    if (isOneOf(nextGraphics.autoProfile, ["low", "balanced", "high", "ultra"])) {
      graphics.autoProfile = nextGraphics.autoProfile.toLowerCase();
    }
    [
      "fbsEnabled",
      "ssaoEnabled",
      "ssaoAvailable",
      "dofAvailable",
      "canChangeFbs",
      "canChangeSsao",
      "canChangeAntiAliasing",
      "canChangeDepthOfField"
    ].forEach(function (key) {
      if (typeof nextGraphics[key] === "boolean") graphics[key] = nextGraphics[key];
    });
  }

  function clampAudioPercentage(value, fallback) {
    var number = Number(value);
    if (!Number.isFinite(number)) return fallback;
    return Math.max(0, Math.min(100, Math.round(number)));
  }

  function mergeAudioState(nextAudio) {
    if (!nextAudio || typeof nextAudio !== "object") return;
    if (typeof nextAudio.enabled === "boolean") {
      state.audio.enabled = nextAudio.enabled;
      state.audioEnabled = nextAudio.enabled;
    }
    state.audio.master = clampAudioPercentage(nextAudio.master, state.audio.master);
    state.audio.music = clampAudioPercentage(nextAudio.music, state.audio.music);
    state.audio.sfx = clampAudioPercentage(nextAudio.sfx, state.audio.sfx);
  }

  function findMissionById(missionId) {
    var missions = state.missions && state.missions.items
      ? state.missions.items
      : [];
    for (var index = 0; index < missions.length; index += 1) {
      if (missions[index].id === missionId) return missions[index];
    }
    return null;
  }

  function missionFallbackItem() {
    var items = state.missions && state.missions.items
      ? state.missions.items
      : [];
    var index;
    for (index = items.length - 1; index >= 0; index -= 1) {
      if (items[index].unlocked && !items[index].completed) return items[index];
    }
    for (index = items.length - 1; index >= 0; index -= 1) {
      if (items[index].unlocked) return items[index];
    }
    return items.length ? items[0] : null;
  }

  function normalizeSelectedMission() {
    var missions = state.missions;
    var selected = findMissionById(missions.selectedId);
    if (selected && selected.unlocked) return selected;
    selected = missionFallbackItem();
    missions.selectedId = selected ? selected.id : "";
    return selected;
  }

  function missionAutoTargetItem() {
    var active = findMissionById(state.missions.activeId);
    if (active && active.unlocked) return active;
    return normalizeSelectedMission() || missionFallbackItem();
  }

  function missionCardForId(missionId) {
    for (var index = 0; index < missionCards.length; index += 1) {
      if (missionCards[index].getAttribute("data-mission-card") === missionId) {
        return missionCards[index];
      }
    }
    return null;
  }

  function centerMissionCard(card) {
    if (!missionGrid || !card) return;
    var maximum = Math.max(0, missionGrid.scrollWidth - missionGrid.clientWidth);
    var target = card.offsetLeft + (card.offsetWidth / 2) - (missionGrid.clientWidth / 2);
    missionGrid.scrollLeft = Math.max(0, Math.min(maximum, target));
  }

  function scheduleMissionAutoScroll(force) {
    if (!missionGrid || !isMissionOpen()) return;
    var targetItem = missionAutoTargetItem();
    if (!targetItem) return;
    if (!force && (missionScrollTouched || missionLastCenteredId === targetItem.id)) return;
    window.clearTimeout(missionAutoScrollTimer);
    missionAutoScrollTimer = window.setTimeout(function () {
      if (!isMissionOpen() || (!force && missionScrollTouched)) return;
      var card = missionCardForId(targetItem.id);
      if (!card) return;
      centerMissionCard(card);
      missionLastCenteredId = targetItem.id;
    }, 0);
  }

  function mergeMissionState(nextMissions) {
    if (!nextMissions || typeof nextMissions !== "object") return;
    var missions = state.missions;
    if (Number.isFinite(Number(nextMissions.totalCount))) {
      missions.totalCount = Math.max(1, Math.min(99, Number(nextMissions.totalCount)));
    }
    if (Number.isFinite(Number(nextMissions.completedCount))) {
      missions.completedCount = Math.max(
        0,
        Math.min(missions.totalCount, Number(nextMissions.completedCount))
      );
    }
    if (Array.isArray(nextMissions.items)) {
      nextMissions.items.forEach(function (nextItem) {
        if (!nextItem || typeof nextItem.id !== "string") return;
        var item = findMissionById(nextItem.id);
        if (!item) return;
        if (Number.isFinite(Number(nextItem.number))) {
          item.number = Math.max(1, Math.min(99, Number(nextItem.number)));
        }
        ["title", "sender", "description"].forEach(function (key) {
          if (typeof nextItem[key] === "string" && nextItem[key].trim()) {
            item[key] = nextItem[key].trim();
          }
        });
        if (
          typeof nextItem.thumbnail === "string" &&
          /^assets\/missions\/[a-z0-9-]+\.webp$/i.test(nextItem.thumbnail)
        ) {
          item.thumbnail = nextItem.thumbnail;
        }
        if (typeof nextItem.unlocked === "boolean") item.unlocked = nextItem.unlocked;
        if (typeof nextItem.completed === "boolean") item.completed = nextItem.completed;
      });
    }
    if (
      typeof nextMissions.selectedId === "string" &&
      findMissionById(nextMissions.selectedId)
    ) {
      missions.selectedId = nextMissions.selectedId;
    }
    if (typeof nextMissions.activeId === "string") {
      var requestedActive = findMissionById(nextMissions.activeId);
      missions.activeId = requestedActive && requestedActive.unlocked
        ? requestedActive.id
        : "";
    }
    var presentationTarget = isMissionOpen() && !missionScrollTouched
      ? missionAutoTargetItem()
      : normalizeSelectedMission();
    if (presentationTarget) missions.selectedId = presentationTarget.id;
  }

  function isGuestAvatar(value) {
    return value === "avatar-1" || value === "avatar-2" || value === "avatar-3";
  }

  function mergeGuestState(nextGuest) {
    if (!nextGuest || typeof nextGuest !== "object") return false;
    var guest = state.guest;
    if (typeof nextGuest.exists === "boolean") guest.exists = nextGuest.exists;
    if (typeof nextGuest.active === "boolean") guest.active = nextGuest.active;
    [
      "id",
      "label",
      "displayName",
      "createdUtc",
      "lastPlayedUtc",
      "response",
      "responseId",
      "message"
    ].forEach(function (key) {
      if (typeof nextGuest[key] === "string") guest[key] = nextGuest[key].trim();
    });
    if (isGuestAvatar(nextGuest.avatar)) guest.avatar = nextGuest.avatar;
    if (typeof nextGuest.progressOnDevice === "boolean") {
      guest.progressOnDevice = nextGuest.progressOnDevice;
    }
    if (!guest.exists) {
      guest.active = false;
      guest.id = "";
      guest.label = "GUEST-LOCAL";
      guest.displayName = "";
    }
    return true;
  }

  function guestAvatarSymbol(avatar) {
    if (avatar === "avatar-2") return "#guest-avatar-masc";
    if (avatar === "avatar-3") return "#guest-avatar-fem";
    return "#guest-avatar-neutral";
  }

  function setGuestAvatar(container, avatar) {
    if (!container) return;
    var use = container.querySelector("use");
    var symbol = guestAvatarSymbol(avatar);
    container.setAttribute("data-avatar", avatar);
    if (!use) return;
    use.setAttribute("href", symbol);
    use.setAttributeNS("http://www.w3.org/1999/xlink", "href", symbol);
  }

  function formatGuestLastPlayed(isoValue) {
    if (!isoValue) return i18n.t("guest.lastNone", "Chưa bắt đầu chơi");
    var parsed = new Date(isoValue);
    if (isNaN(parsed.getTime())) {
      return i18n.t("guest.lastSaved", "Tiến trình đã lưu trên máy");
    }
    var now = new Date();
    if (
      parsed.getFullYear() === now.getFullYear() &&
      parsed.getMonth() === now.getMonth() &&
      parsed.getDate() === now.getDate()
    ) {
      return i18n.t("guest.lastToday", "Chơi lần cuối: Hôm nay");
    }
    var locale = i18n.getLocale ? i18n.getLocale() : "vi";
    var browserLocale = locale === "zh-Hans" ? "zh-CN" : locale;
    var localizedDate;
    try {
      localizedDate = parsed.toLocaleDateString(browserLocale, {
        year: "numeric",
        month: "2-digit",
        day: "2-digit"
      });
    } catch (error) {
      var day = parsed.getDate() < 10 ? "0" + parsed.getDate() : String(parsed.getDate());
      var month = parsed.getMonth() + 1;
      month = month < 10 ? "0" + month : String(month);
      localizedDate = day + "/" + month + "/" + parsed.getFullYear();
    }
    return i18n.t(
      "guest.lastDate",
      "Chơi lần cuối: {date}",
      { date: localizedDate }
    );
  }

  function profileLabel(profile) {
    switch (profile) {
      case "low": return i18n.t("settings.profile.low", "THẤP");
      case "high": return i18n.t("settings.profile.high", "CAO");
      case "ultra": return i18n.t("settings.profile.ultra", "RẤT CAO");
      default: return i18n.t("settings.profile.balanced", "VỪA");
    }
  }

  function qualityDescription(quality) {
    switch (quality) {
      case "low": return i18n.t("settings.quality.lowDetail", "ƯU TIÊN PIN • 30 FPS");
      case "high": return i18n.t("settings.quality.highDetail", "CHẤT LƯỢNG CAO • 60 FPS • TỐN PIN HƠN");
      default: return i18n.t("settings.quality.balancedDetail", "CÂN BẰNG • 60 FPS");
    }
  }

  function setControlAvailability(control, available) {
    control.disabled = !available;
    control.setAttribute("aria-disabled", String(!available));
  }

  function setSettingsView(view, focusTab) {
    if (["graphics", "audio", "language"].indexOf(view) < 0) view = "graphics";
    settingsView = view;
    if (settingsPanel) settingsPanel.setAttribute("data-settings-view", view);
    settingsTabs.forEach(function (tab) {
      var active = tab.getAttribute("data-settings-tab") === view;
      tab.classList.toggle("is-active", active);
      tab.setAttribute("aria-selected", String(active));
      tab.setAttribute("tabindex", active ? "0" : "-1");
      if (active && focusTab) tab.focus();
    });
    settingsPanes.forEach(function (pane) {
      var active = pane.getAttribute("data-settings-pane") === view;
      pane.hidden = !active;
      pane.setAttribute("aria-hidden", String(!active));
    });
  }

  function renderAudioSettings() {
    state.audio.enabled = state.audioEnabled;
    if (settingsAudioMaster) {
      settingsAudioMaster.classList.toggle("is-on", state.audio.enabled);
      settingsAudioMaster.setAttribute("aria-checked", String(state.audio.enabled));
      settingsAudioMaster.setAttribute(
        "aria-label",
        i18n.t("settings.audio.title", "ÂM THANH MENU")
      );
      var enabledLabel = settingsAudioMaster.querySelector("[data-audio-enabled-label]");
      if (enabledLabel) {
        enabledLabel.textContent = i18n.t(
          state.audio.enabled ? "common.on" : "common.off",
          state.audio.enabled ? "BẬT" : "TẮT"
        );
      }
    }
    settingsAudioRanges.forEach(function (range) {
      var channel = range.getAttribute("data-audio-range");
      var value = clampAudioPercentage(state.audio[channel], 100);
      range.value = String(value);
      range.setAttribute(
        "aria-label",
        i18n.t("settings.audio." + channel, range.getAttribute("aria-label"))
      );
      range.setAttribute("aria-valuetext", value + "%");
      var visual = range.parentNode.querySelector("span i");
      var output = range.parentNode.querySelector("[data-audio-output]");
      if (visual) visual.style.width = value + "%";
      if (output) output.textContent = value + "%";
    });
  }

  function renderLanguageSettings() {
    var group = settingsScreen
      ? settingsScreen.querySelector(".settings-language-list")
      : null;
    if (group) {
      group.setAttribute(
        "aria-label",
        i18n.t("settings.language.title", "NGÔN NGỮ GIAO DIỆN")
      );
    }
    settingsLanguageButtons.forEach(function (button) {
      var active = button.getAttribute("data-language") === state.language;
      button.classList.toggle("is-active", active);
      button.setAttribute("aria-checked", String(active));
      button.setAttribute("tabindex", active ? "0" : "-1");
    });
  }

  function renderSettings() {
    if (!settingsScreen) return;
    var graphics = state.graphics;
    setSettingsView(settingsView, false);
    renderAudioSettings();
    renderLanguageSettings();

    settingsControls.forEach(function (control) {
      var setting = control.getAttribute("data-setting");
      var value = control.getAttribute("data-value");
      var active = false;
      var available = true;

      if (setting === "quality") {
        active = value === graphics.quality;
      } else if (setting === "aa") {
        active = value === graphics.antiAliasing;
        available = graphics.canChangeAntiAliasing;
      } else if (setting === "dof") {
        active = value === graphics.depthOfField;
        available = graphics.dofAvailable && graphics.canChangeDepthOfField;
      } else if (setting === "fbs") {
        active = graphics.fbsEnabled;
        available = graphics.canChangeFbs;
      } else if (setting === "ssao") {
        active = graphics.ssaoEnabled;
        available = graphics.ssaoAvailable && graphics.canChangeSsao;
      }

      control.classList.toggle("is-active", active);
      control.classList.toggle("is-on", active);
      if (control.getAttribute("role") === "radio") {
        control.setAttribute("aria-checked", String(active));
        control.setAttribute("tabindex", active ? "0" : "-1");
      } else if (control.getAttribute("role") === "switch") {
        control.setAttribute("aria-checked", String(active));
        var switchLabel = control.querySelector(".settings-switch__label");
        if (switchLabel) {
          switchLabel.textContent = setting === "ssao" && !graphics.ssaoAvailable
            ? "N/A"
            : i18n.t(active ? "common.on" : "common.off", active ? "BẬT" : "TẮT");
        }
      }
      setControlAvailability(control, available || setting === "quality");
    });

    Array.prototype.slice.call(settingsScreen.querySelectorAll("[data-lock-hint]"))
      .forEach(function (hint) {
        var setting = hint.getAttribute("data-lock-hint");
        var locked = false;
        if (setting === "aa") locked = !graphics.canChangeAntiAliasing;
        if (setting === "dof") {
          locked = !graphics.dofAvailable || !graphics.canChangeDepthOfField;
        }
        if (setting === "fbs") locked = !graphics.canChangeFbs;
        if (setting === "ssao") {
          locked = !graphics.ssaoAvailable || !graphics.canChangeSsao;
        }
        hint.classList.toggle("is-visible", locked);
        hint.hidden = !locked;
        hint.setAttribute("aria-hidden", String(!locked));
        if (setting === "ssao" && !graphics.ssaoAvailable) {
          hint.textContent = i18n.t(
            "settings.lock.unsupported",
            "KHÔNG HỖ TRỢ TRÊN THIẾT BỊ"
          );
        } else if (setting === "aa" || setting === "dof") {
          hint.textContent = i18n.t(
            "settings.lock.effect",
            "Preset đang quản lý hiệu ứng"
          );
        } else {
          hint.textContent = i18n.t(
            "settings.lock.preset",
            "Được quản lý bởi preset"
          );
        }
      });

    if (qualitySummary) {
      qualitySummary.innerHTML = graphics.quality === "auto"
        ? "<strong>AUTO</strong> " + i18n.t("settings.quality.using", "ĐANG DÙNG:") + " <b>" +
          profileLabel(graphics.autoProfile) +
          "</b><i aria-hidden=\"true\"></i>" +
          i18n.t("settings.quality.autoDetail", "TỰ CÂN BẰNG THEO FPS")
        : "<strong>PRESET</strong> " + i18n.t("settings.quality.using", "ĐANG DÙNG:") + " <b>" +
          profileLabel(graphics.quality) +
          "</b><i aria-hidden=\"true\"></i>" +
          qualityDescription(graphics.quality);
    }
  }

  function missionStatusLabel(item, selected) {
    if (!item.unlocked) return i18n.t("mission.locked", "KHÓA");
    if (item.completed) return i18n.t("mission.completed", "ĐÃ HOÀN THÀNH");
    return selected
      ? i18n.t("mission.selected", "ĐANG CHỌN")
      : i18n.t("mission.unlocked", "ĐÃ MỞ KHÓA");
  }

  function twoDigit(value) {
    var normalized = Math.max(0, Math.floor(Number(value) || 0));
    return normalized < 10 ? "0" + normalized : String(normalized);
  }

  function renderMissions() {
    if (!missionScreen) return;
    var missions = state.missions;
    var selected = normalizeSelectedMission();

    if (missionProgressValue) {
      missionProgressValue.textContent = missions.completedCount + "/" + missions.totalCount;
    }
    if (missionProgress) {
      missionProgress.setAttribute(
        "aria-label",
        i18n.t("mission.progress", "TIẾN ĐỘ")
      );
      missionProgress.setAttribute("aria-valuemax", String(missions.totalCount));
      missionProgress.setAttribute("aria-valuenow", String(missions.completedCount));
      missionProgress.setAttribute(
        "aria-valuetext",
        i18n.t(
          "mission.progressText",
          "Đã hoàn thành {done} trên {total} màn",
          { done: missions.completedCount, total: missions.totalCount }
        )
      );
      Array.prototype.slice.call(
        missionProgress.querySelectorAll(".mission-progress__segments i")
      ).forEach(function (segment, index) {
        segment.classList.toggle("is-completed", index < missions.completedCount);
      });
    }

    missionCards.forEach(function (card, cardIndex) {
      var item = findMissionById(card.getAttribute("data-mission-card"));
      if (!item) return;
      var displayItem = i18n.localizeMission(item);
      var isSelected = selected && item.id === selected.id;
      var status = missionStatusLabel(item, isSelected);
      card.classList.toggle("is-selected", isSelected);
      card.classList.toggle("is-locked", !item.unlocked);
      card.classList.toggle("is-unlocked", item.unlocked && !item.completed);
      card.classList.toggle("is-completed", item.completed);
      card.setAttribute("aria-checked", String(isSelected));
      card.setAttribute("aria-disabled", String(!item.unlocked));
      card.setAttribute("tabindex", isSelected ? "0" : "-1");
      card.setAttribute(
        "aria-label",
        i18n.t("mission.level", "MÀN") + " " + twoDigit(item.number) +
          ", " + displayItem.title + ", " + status
      );
      var number = card.querySelector(".mission-card__number");
      var title = card.querySelector("[data-mission-title]");
      var statusLabel = card.querySelector("[data-mission-status]");
      var image = card.querySelector("img");
      if (number) {
        number.textContent = i18n.t("mission.level", "MÀN") + " " + twoDigit(item.number);
      }
      if (title) title.textContent = displayItem.title;
      if (statusLabel) statusLabel.textContent = status;
      if (image && image.getAttribute("src") !== item.thumbnail) image.src = item.thumbnail;
      var connector = card.querySelector(".mission-card__connector");
      if (connector) {
        connector.classList.toggle(
          "is-completed",
          cardIndex < missions.items.length - 1 && item.completed
        );
      }
    });

    if (!selected) return;
    var selectedDisplay = i18n.localizeMission(selected);
    if (missionDetailNumber) {
      missionDetailNumber.textContent = twoDigit(selected.number);
    }
    if (missionDetailSender) {
      missionDetailSender.textContent = i18n.t("mission.from", "NHIỆM VỤ TỪ") + " " + selected.sender;
    }
    if (missionDetailTitle) missionDetailTitle.textContent = selectedDisplay.title;
    if (missionDetailDescription) {
      missionDetailDescription.textContent = selectedDisplay.description;
    }
    if (missionDetailState) {
      missionDetailState.textContent = selected.completed
        ? i18n.t("mission.completed", "ĐÃ HOÀN THÀNH")
        : i18n.t("mission.ready", "SẴN SÀNG");
      missionDetailState.classList.toggle("is-locked", !selected.unlocked);
    }
    if (missionPlayButton) {
      missionPlayButton.disabled = missionPlayPending || menuBusy || !selected.unlocked;
      missionPlayButton.setAttribute(
        "aria-disabled",
        String(missionPlayButton.disabled)
      );
      missionPlayButton.setAttribute(
        "aria-label",
        i18n.t("mission.play", "CHƠI") + " " + selectedDisplay.title
      );
    }
    if (missionHelp && !missionHelp.classList.contains("is-warning")) {
      var helpCopy = missionHelp.querySelector("span");
      if (helpCopy) {
        helpCopy.textContent = missions.completedCount >= missions.totalCount
          ? i18n.t("mission.allComplete", "Bạn đã hoàn thành toàn bộ màn chơi")
          : i18n.t("mission.help", "Hoàn thành màn trước để mở khóa màn tiếp theo");
      }
    }
    scheduleMissionAutoScroll(false);
  }

  function setGuestFeedback(message, isError) {
    if (!guestFeedback) return;
    guestFeedback.textContent = message || "";
    guestFeedback.classList.toggle("is-error", Boolean(isError));
  }

  function renderGuest() {
    if (!guestScreen) return;
    var guest = state.guest;
    var createMode = guestMode === "create";

    guestModeButtons.forEach(function (button) {
      var mode = button.getAttribute("data-guest-mode");
      var active = mode === guestMode;
      button.classList.toggle("is-active", active);
      button.setAttribute("aria-pressed", String(active));
      button.setAttribute("aria-disabled", String(mode === "login" && !guest.exists));
      button.setAttribute("tabindex", active ? "0" : "-1");
    });

    if (guestCreatePane) {
      guestCreatePane.classList.toggle("is-inactive", !createMode);
      guestCreatePane.setAttribute("aria-hidden", String(!createMode));
    }
    if (guestLoginPane) {
      guestLoginPane.classList.toggle("is-inactive", createMode);
      guestLoginPane.setAttribute("aria-hidden", String(createMode));
    }

    if (guestNameInput) {
      guestNameInput.disabled = !createMode || Boolean(guestRequestPending) || menuBusy;
    }
    guestAvatarButtons.forEach(function (button) {
      var avatar = button.getAttribute("data-guest-avatar");
      var selected = avatar === guestDraftAvatar;
      button.classList.toggle("is-selected", selected);
      button.setAttribute("aria-checked", String(selected));
      button.setAttribute("tabindex", createMode && selected ? "0" : "-1");
      button.disabled = !createMode || Boolean(guestRequestPending) || menuBusy;
    });

    setGuestAvatar(
      guestScreen.querySelector("[data-guest-avatar-preview]"),
      guestDraftAvatar
    );
    setGuestAvatar(
      guestScreen.querySelector("[data-guest-profile-avatar]"),
      guest.avatar
    );

    if (guestProfileId) {
      guestProfileId.textContent = guest.exists
        ? (guest.label || "GUEST-LOCAL")
        : i18n.t("guest.empty", "CHƯA CÓ HỒ SƠ");
    }
    if (guestProfileName) {
      guestProfileName.textContent = guest.exists
        ? (guest.displayName || i18n.t("guest.player", "NGƯỜI CHƠI"))
        : i18n.t("guest.emptyHint", "Tạo hồ sơ khách để bắt đầu");
    }
    var guestProfileCard = guestScreen.querySelector(".guest-profile-card");
    if (guestProfileCard) {
      guestProfileCard.classList.toggle("has-profile", Boolean(guest.exists));
    }
    if (guestLastPlayed) {
      guestLastPlayed.textContent = formatGuestLastPlayed(guest.lastPlayedUtc);
    }
    if (guestCreateLabel) {
      guestCreateLabel.textContent = guest.exists
        ? i18n.t("guest.update", "CẬP NHẬT HỒ SƠ")
        : i18n.t("guest.create", "TẠO HỒ SƠ");
    }
    if (guestCreateButton) {
      guestCreateButton.disabled = !createMode || Boolean(guestRequestPending) || menuBusy;
      guestCreateButton.setAttribute("aria-disabled", String(guestCreateButton.disabled));
    }
    if (guestLoginButton) {
      guestLoginButton.disabled =
        createMode || !guest.exists || Boolean(guestRequestPending) || menuBusy;
      guestLoginButton.setAttribute("aria-disabled", String(guestLoginButton.disabled));
    }
  }

  function applyLocalQualityPreset(quality) {
    var graphics = state.graphics;
    graphics.quality = quality;
    graphics.canChangeFbs = quality === "auto";
    graphics.canChangeSsao = quality === "auto" && graphics.ssaoAvailable;
    graphics.canChangeAntiAliasing = quality === "auto";
    graphics.canChangeDepthOfField =
      graphics.dofAvailable && (quality === "auto" || quality === "high");

    if (quality === "low") {
      graphics.fbsEnabled = false;
      graphics.ssaoEnabled = false;
      graphics.antiAliasing = "off";
      graphics.depthOfField = "off";
    } else if (quality === "balanced") {
      graphics.fbsEnabled = true;
      graphics.ssaoEnabled = false;
      graphics.antiAliasing = "off";
      graphics.depthOfField = "off";
    } else if (quality === "high") {
      graphics.fbsEnabled = true;
      graphics.ssaoEnabled = true;
      graphics.antiAliasing = "smaa";
      if (graphics.depthOfField === "off") graphics.depthOfField = "far";
    }
  }

  function setState(nextState) {
    var normalized = normalizeState(nextState);
    if (!normalized) return false;
    var closeGuestAfterState = false;
    var focusGuestAfterState = "";

    if (typeof normalized.version === "string" && normalized.version.trim()) {
      state.version = normalized.version.charAt(0).toLowerCase() === "v"
        ? normalized.version
        : "v" + normalized.version;
    }
    if (typeof normalized.hasSave === "boolean") state.hasSave = normalized.hasSave;
    if (typeof normalized.sync === "string") state.sync = normalized.sync.toLowerCase();
    if (typeof normalized.profile === "string") state.profile = normalized.profile;
    if (typeof normalized.showQuit === "boolean") state.showQuit = normalized.showQuit;
    if (typeof normalized.audioEnabled === "boolean") {
      state.audioEnabled = normalized.audioEnabled;
      state.audio.enabled = normalized.audioEnabled;
    }
    mergeAudioState(normalized.audio);
    if (typeof normalized.language === "string") {
      state.language = i18n.normalize(normalized.language);
    }
    i18n.apply(state.language);
    mergeGraphicsState(normalized.graphics);
    mergeMissionState(normalized.missions);
    var receivedGuestState = mergeGuestState(normalized.guest);

    if (receivedGuestState && guestRequestPending) {
      var completedGuestAction = guestRequestPending;
      var guestResponse = state.guest.response;
      var responseMatchesRequest =
        state.guest.responseId &&
        state.guest.responseId === guestPendingRequestId;
      var responseMatchesCreate =
        responseMatchesRequest &&
        completedGuestAction === "guest-create" &&
        guestResponse === "created";
      var responseMatchesLogin =
        responseMatchesRequest &&
        completedGuestAction === "guest-login" &&
        guestResponse === "logged-in";
      if (responseMatchesRequest && guestResponse === "error") {
        guestRequestPending = "";
        guestPendingRequestId = "";
        window.clearTimeout(guestRequestTimer);
        if (guestPanel) guestPanel.setAttribute("aria-busy", "false");
        setGuestFeedback(
          localizeMessageToken(
            state.guest.message,
            "guest.response.unconfirmed",
            "Không thể xác nhận hồ sơ khách."
          ),
          true
        );
        focusGuestAfterState = completedGuestAction === "guest-create"
          ? "create-error"
          : "login-error";
      } else if (responseMatchesCreate && state.guest.exists) {
        guestRequestPending = "";
        guestPendingRequestId = "";
        window.clearTimeout(guestRequestTimer);
        if (guestPanel) guestPanel.setAttribute("aria-busy", "false");
        guestMode = "login";
        guestDraftAvatar = state.guest.avatar;
        if (guestNameInput) guestNameInput.value = state.guest.displayName;
        setGuestFeedback(
          localizeMessageToken(
            state.guest.message,
            "guest.response.saved",
            "Hồ sơ khách đã được lưu trên thiết bị."
          ),
          false
        );
        focusGuestAfterState = "created";
      } else if (responseMatchesLogin && state.guest.exists) {
        guestRequestPending = "";
        guestPendingRequestId = "";
        window.clearTimeout(guestRequestTimer);
        if (guestPanel) guestPanel.setAttribute("aria-busy", "false");
        closeGuestAfterState = true;
      }
    }

    versionLabel.textContent = state.version;
    syncLabel.textContent = state.sync === "online"
      ? i18n.t("sync.online", "ĐÃ KẾT NỐI")
      : i18n.t("sync.offline", "OFFLINE");
    syncLabel.style.color = state.sync === "online" ? "var(--cyan)" : "var(--purple)";
    continueButton.disabled = !state.hasSave;
    continueButton.setAttribute("aria-disabled", String(!state.hasSave));
    if (quitButton) quitButton.hidden = !state.showQuit;
    menuAudio.setEnabled(state.audioEnabled, false);
    menuAudio.setMix(state.audio, false);
    updateAudioToggle();
    var selectedMainButton = document.querySelector(".tech-button.is-selected");
    if (!state.hasSave) {
      continueButton.classList.remove("is-selected");
      if (!userInteracted || !selectedMainButton || selectedMainButton.disabled) {
        selectButton(newGameButton);
      }
    } else if (!userInteracted) {
      selectButton(continueButton);
    }
    renderSettings();
    renderMissions();
    renderGuest();
    renderOnline(true);
    if (focusGuestAfterState) {
      window.setTimeout(function () {
        if (!isGuestOpen()) return;
        if (focusGuestAfterState === "create-error" && guestNameInput) {
          guestNameInput.focus();
        } else if (focusGuestAfterState === "created" && guestLoginButton) {
          guestLoginButton.focus();
        } else {
          var activeMode = guestScreen.querySelector(".guest-mode.is-active");
          if (activeMode) activeMode.focus();
          else if (guestPanel) guestPanel.focus();
        }
      }, 0);
    }
    if (closeGuestAfterState) {
      window.setTimeout(function () {
        closeGuestScreen(true);
      }, 0);
    }
    return true;
  }

  function updateAudioToggle() {
    if (!audioToggle) return;
    audioToggle.classList.toggle("is-muted", !state.audioEnabled);
    audioToggle.setAttribute("aria-pressed", String(state.audioEnabled));
    audioToggle.setAttribute("aria-label", i18n.t("main.audio", "Âm thanh"));
    if (audioStateLabel) {
      audioStateLabel.textContent = i18n.t(
        state.audioEnabled ? "common.on" : "common.off",
        state.audioEnabled ? "BẬT" : "TẮT"
      );
    }
  }

  function showToast(message, tone) {
    if (!toast || !message) return;
    if (tone === "error") menuAudio.play("error");
    if (tone === "success") menuAudio.play("notify");
    window.clearTimeout(toastTimer);
    toast.textContent = message;
    toast.classList.add("is-visible");
    toastTimer = window.setTimeout(function () {
      toast.classList.remove("is-visible");
    }, 1800);
  }

  function setBusy(nextBusy) {
    var busy = Boolean(nextBusy && nextBusy.busy);
    var message = nextBusy && typeof nextBusy.message === "string"
      ? nextBusy.message.trim()
      : "";
    menuBusy = busy;
    if (!busy) {
      missionPlayPending = false;
      window.clearTimeout(missionPlayTimer);
    }
    document.documentElement.classList.toggle("is-busy", busy);
    document.body.setAttribute("aria-busy", String(busy));
    menuAudio.setDucked(busy);
    if (busyLabel) {
      busyLabel.textContent = message ||
        i18n.t("runtime.busy.default", "ĐANG XỬ LÝ...");
    }
    if (busyOverlay) busyOverlay.setAttribute("aria-hidden", String(!busy));
    renderMissions();
    renderGuest();
  }

  function setHostMetrics(nextMetrics) {
    if (!nextMetrics || typeof nextMetrics !== "object") return false;
    ["safeLeft", "safeRight", "safeTop", "safeBottom"].forEach(function (key) {
      var value = Number(nextMetrics[key]);
      if (Number.isFinite(value)) {
        hostMetrics[key] = Math.max(0, Math.min(0.5, value));
      }
    });
    requestLayout();
    return true;
  }

  function sendToHost(action, payload) {
    var message = JSON.stringify({
      version: MESSAGE_VERSION,
      action: action,
      payload: JSON.stringify(payload || {}),
      timestamp: Date.now()
    });
    var delivered = false;

    try {
      if (window.FranklinUnity && typeof window.FranklinUnity.postMessage === "function") {
        window.FranklinUnity.postMessage(message);
        delivered = true;
      } else if (window.Unity && typeof window.Unity.call === "function") {
        window.Unity.call(message);
        delivered = true;
      } else if (
        window.webkit &&
        window.webkit.messageHandlers &&
        window.webkit.messageHandlers.FranklinMenu
      ) {
        window.webkit.messageHandlers.FranklinMenu.postMessage(message);
        delivered = true;
      } else if (window.unityInstance && typeof window.unityInstance.SendMessage === "function") {
        window.unityInstance.SendMessage("FranklinWebMenuBridge", "OnWebMessage", message);
        delivered = true;
      }
    } catch (error) {
      delivered = false;
    }

    window.dispatchEvent(new CustomEvent("franklin:menu-action", {
      detail: { action: action, payload: payload || {}, delivered: delivered }
    }));

    if (!delivered && action !== "ready" && action !== "audio-toggle") {
      showToast(i18n.t("runtime.unityDisconnected", "Không kết nối được Unity."), "error");
    }
    return delivered;
  }

  function selectButton(button) {
    if (!button || button.disabled || !button.classList.contains("tech-button")) return;
    actionButtons.forEach(function (candidate) {
      if (candidate.classList.contains("tech-button")) {
        candidate.classList.toggle("is-selected", candidate === button);
      }
    });
  }

  function playActionSound(action) {
    switch (action) {
      case "continue":
        // Unity validates Continue before triggering the transition cue.
        break;
      case "new-game":
      case "mission-open":
      case "online-open":
      case "settings":
      case "profile":
        menuAudio.play("panel");
        break;
      case "sync":
        menuAudio.play(state.sync === "online" ? "notify" : "denied");
        break;
      case "quit":
        menuAudio.play("back");
        break;
      default:
        menuAudio.play("activate");
        break;
    }
  }

  function setMainMenuAccessibility(hidden) {
    [".top-actions", ".menu-column", ".menu-footer", ".audio-toggle"]
      .forEach(function (selector) {
        var element = document.querySelector(selector);
        if (!element) return;
        if (hidden) element.setAttribute("aria-hidden", "true");
        else element.removeAttribute("aria-hidden");
      });
    actionButtons.forEach(function (button) {
      if (hidden) button.setAttribute("tabindex", "-1");
      else button.removeAttribute("tabindex");
    });
  }

  function openSettings() {
    if (
      !settingsScreen ||
      !settingsScreen.hidden ||
      isGuestOpen() ||
      isMissionOpen() ||
      isOnlineOpen()
    ) return;
    settingsFocusBeforeOpen = document.activeElement;
    settingsScreen.hidden = false;
    settingsScreen.setAttribute("aria-hidden", "false");
    document.documentElement.classList.add("is-settings-open");
    setMainMenuAccessibility(true);
    renderSettings();
    window.setTimeout(function () {
      var closeButton = settingsScreen.querySelector(".settings-close");
      if (closeButton) closeButton.focus();
      else if (settingsPanel) settingsPanel.focus();
    }, 0);
  }

  function closeSettings(complete) {
    if (!settingsScreen || settingsScreen.hidden) return false;
    settingsScreen.hidden = true;
    settingsScreen.setAttribute("aria-hidden", "true");
    document.documentElement.classList.remove("is-settings-open");
    setMainMenuAccessibility(false);
    if (complete) sendToHost("settings-complete", {});
    var focusTarget = settingsFocusBeforeOpen && document.contains(settingsFocusBeforeOpen)
      ? settingsFocusBeforeOpen
      : settingsMenuButton;
    settingsFocusBeforeOpen = null;
    if (focusTarget && !focusTarget.disabled) focusTarget.focus();
    return true;
  }

  function isSettingsOpen() {
    return Boolean(settingsScreen && !settingsScreen.hidden);
  }

  function isGuestOpen() {
    return Boolean(guestScreen && !guestScreen.hidden);
  }

  function openGuestScreen() {
    if (
      !guestScreen ||
      !guestScreen.hidden ||
      isSettingsOpen() ||
      isMissionOpen() ||
      isOnlineOpen()
    ) {
      return false;
    }
    guestFocusBeforeOpen = document.activeElement;
    guestMode = state.guest.exists ? "login" : "create";
    guestDraftAvatar = isGuestAvatar(state.guest.avatar)
      ? state.guest.avatar
      : "avatar-1";
    guestRequestPending = "";
    guestPendingRequestId = "";
    window.clearTimeout(guestRequestTimer);
    if (guestPanel) guestPanel.setAttribute("aria-busy", "false");
    setGuestFeedback("", false);
    if (guestNameInput) {
      guestNameInput.value = state.guest.displayName || "";
      guestNameInput.setAttribute("aria-invalid", "false");
    }
    guestScreen.hidden = false;
    guestScreen.setAttribute("aria-hidden", "false");
    document.documentElement.classList.add("is-guest-open");
    setMainMenuAccessibility(true);
    renderGuest();
    window.setTimeout(function () {
      var activeMode = guestScreen.querySelector(".guest-mode.is-active");
      if (activeMode) activeMode.focus();
      else if (guestBackButton) guestBackButton.focus();
      else if (guestPanel) guestPanel.focus();
    }, 0);
    return true;
  }

  function closeGuestScreen() {
    if (!isGuestOpen() || menuBusy || guestRequestPending) return false;
    if (guestNameInput && document.activeElement === guestNameInput) {
      guestNameInput.blur();
    }
    guestScreen.hidden = true;
    guestScreen.setAttribute("aria-hidden", "true");
    if (guestPanel) guestPanel.setAttribute("aria-busy", "false");
    document.documentElement.classList.remove("is-guest-open");
    document.documentElement.classList.remove("is-guest-keyboard-open");
    guestKeyboardScale = 0;
    guestKeyboardViewportWidth = 0;
    setMainMenuAccessibility(false);
    setGuestFeedback("", false);
    var focusTarget = guestFocusBeforeOpen && document.contains(guestFocusBeforeOpen)
      ? guestFocusBeforeOpen
      : profileMenuButton;
    guestFocusBeforeOpen = null;
    if (focusTarget && !focusTarget.disabled) focusTarget.focus();
    requestLayout();
    return true;
  }

  function openMissions(opener) {
    if (
      !missionScreen ||
      !missionScreen.hidden ||
      isSettingsOpen() ||
      isGuestOpen() ||
      isOnlineOpen()
    ) return false;
    missionFocusBeforeOpen = opener && document.contains(opener)
      ? opener
      : document.activeElement;
    missionScreen.hidden = false;
    missionScreen.setAttribute("aria-hidden", "false");
    document.documentElement.classList.add("is-mission-open");
    setMainMenuAccessibility(true);
    missionScrollTouched = false;
    missionLastCenteredId = "";
    missionSwipeGesture = false;
    missionSwipeClickUntil = 0;
    var openingTarget = missionAutoTargetItem();
    if (openingTarget) state.missions.selectedId = openingTarget.id;
    renderMissions();
    window.setTimeout(function () {
      var selectedCard = missionScreen.querySelector(".mission-card.is-selected");
      if (selectedCard) selectedCard.focus();
      else if (missionPanel) missionPanel.focus();
      scheduleMissionAutoScroll(true);
    }, 0);
    return true;
  }

  function closeMissions() {
    if (!missionScreen || missionScreen.hidden || menuBusy || missionPlayPending) {
      return false;
    }
    window.clearTimeout(missionHelpTimer);
    window.clearTimeout(missionAutoScrollTimer);
    missionScreen.hidden = true;
    missionScreen.setAttribute("aria-hidden", "true");
    document.documentElement.classList.remove("is-mission-open");
    setMainMenuAccessibility(false);
    if (missionHelp) missionHelp.classList.remove("is-warning");
    var focusTarget = missionFocusBeforeOpen && document.contains(missionFocusBeforeOpen)
      ? missionFocusBeforeOpen
      : (missionMenuButton || newGameButton);
    missionFocusBeforeOpen = null;
    if (focusTarget && !focusTarget.disabled) focusTarget.focus();
    return true;
  }

  function isMissionOpen() {
    return Boolean(missionScreen && !missionScreen.hidden);
  }

  function isOnlineOpen() {
    return Boolean(onlineScreen && !onlineScreen.hidden);
  }

  function clearOnlineSearchTimers(invalidateRun) {
    window.clearInterval(onlineSearchInterval);
    window.clearTimeout(onlineSearchTimeout);
    onlineSearchInterval = 0;
    onlineSearchTimeout = 0;
    if (invalidateRun) onlineSearchRunId += 1;
  }

  function onlineClock(seconds) {
    var safeSeconds = Math.max(0, Math.min(30, Math.floor(seconds)));
    return "00:" + (safeSeconds < 10 ? "0" + safeSeconds : String(safeSeconds));
  }

  function createOnlinePlayers() {
    var callsigns = [
      "BOT-NOVA-17",
      "BOT-RAVEN-04",
      "BOT-ECHO-29",
      "BOT-ORBIT-21",
      "BOT-VEX-08",
      "BOT-NEON-32"
    ];
    var index;
    for (index = callsigns.length - 1; index > 0; index -= 1) {
      var swapIndex = Math.floor(Math.random() * (index + 1));
      var swap = callsigns[index];
      callsigns[index] = callsigns[swapIndex];
      callsigns[swapIndex] = swap;
    }
    var localName = state.guest && state.guest.exists && state.guest.displayName
      ? state.guest.displayName
      : i18n.t("online.you", "BẠN");
    onlinePlayers = [{ name: localName, ping: 24, ready: true, local: true }];
    for (index = 0; index < 3; index += 1) {
      onlinePlayers.push({
        name: callsigns[index],
        ping: 28 + Math.floor(Math.random() * 65),
        ready: false,
        local: false
      });
    }
  }

  function updateOnlinePlayerJoins(elapsedMilliseconds) {
    if (!onlineSearchDuration || !onlinePlayers.length) return;
    var ratios = [0, 0.28, 0.56, 1];
    onlinePlayers.forEach(function (player, index) {
      if (index === 0 || elapsedMilliseconds >= onlineSearchDuration * ratios[index]) {
        player.ready = true;
      }
    });
  }

  function renderOnlinePlayer(slot, player) {
    if (!slot || !player) return;
    var nameNode = slot.querySelector("[data-online-name]");
    var stateNode = slot.querySelector("[data-online-state]");
    var pingNode = slot.querySelector("[data-online-ping]");
    var ready = Boolean(player.ready);
    var stateCopy = ready
      ? (player.local
        ? i18n.t("online.youReady", "BẠN • SẴN SÀNG")
        : i18n.t("online.botReady", "BOT MÔ PHỎNG • SẴN SÀNG"))
      : i18n.t("online.waiting", "ĐANG TÌM...");
    var displayName = player.local && !(state.guest && state.guest.displayName)
      ? i18n.t("online.you", "BẠN")
      : player.name;
    if (nameNode) nameNode.textContent = ready ? displayName : "---";
    if (stateNode) stateNode.textContent = stateCopy;
    if (pingNode) pingNode.textContent = ready ? player.ping + " ms" : "-- ms";
    slot.classList.toggle("is-ready", ready);
    slot.classList.toggle("is-waiting", !ready);
    slot.setAttribute(
      "aria-label",
      (ready ? displayName : i18n.t("online.emptySlot", "Vị trí đang tìm")) +
        ", " + stateCopy + (ready ? ", " + player.ping + " ms" : "")
    );
  }

  function renderOnline(force) {
    if (!onlineScreen) return;
    var now = Date.now();
    var elapsedMilliseconds = onlineMode === "searching"
      ? Math.max(0, Math.min(30000, now - onlineSearchStartedAt))
      : (onlineMode === "matched" ? Math.min(30000, onlineSearchDuration) : 0);
    if (onlineMode === "searching") updateOnlinePlayerJoins(elapsedMilliseconds);
    if (onlineMode === "matched") {
      onlinePlayers.forEach(function (player) { player.ready = true; });
    }
    var elapsedSeconds = Math.min(30, Math.floor(elapsedMilliseconds / 1000));
    if (!force && onlineLastRenderedSecond === elapsedSeconds && onlineMode !== "searching") {
      return;
    }
    onlineLastRenderedSecond = elapsedSeconds;
    onlineScreen.classList.toggle("is-searching", onlineMode === "searching");
    onlineScreen.classList.toggle("is-matched", onlineMode === "matched");
    onlineScreen.classList.toggle("is-cancelled", onlineMode === "cancelled");
    onlineScreen.setAttribute("data-online-state", onlineMode);

    var statusKey = "online.searching";
    var statusFallback = "ĐANG TÌM PHÒNG...";
    var detailKey = "online.searchingHint";
    var detailFallback = "Đang quét các phiên chơi phù hợp trong khu vực";
    if (onlineMode === "matched") {
      statusKey = "online.found";
      statusFallback = "ĐÃ TẠO NHÓM MÔ PHỎNG";
      detailKey = "online.foundHint";
      detailFallback = "Nhóm mô phỏng đã sẵn sàng";
    } else if (onlineMode === "cancelled") {
      statusKey = "online.cancelled";
      statusFallback = "ĐÃ DỪNG TÌM KIẾM";
      detailKey = "online.cancelledHint";
      detailFallback = "Bạn có thể thử tìm lại bất cứ lúc nào";
    }
    if (onlineStatus) onlineStatus.textContent = i18n.t(statusKey, statusFallback);
    if (onlineStatusDetail) {
      onlineStatusDetail.textContent = i18n.t(detailKey, detailFallback);
    }
    if (onlineTime) onlineTime.textContent = onlineClock(elapsedSeconds);
    var progressPercent = onlineMode === "matched"
      ? 100
      : Math.max(0, Math.min(100, elapsedMilliseconds / 30000 * 100));
    if (onlineProgressBar) onlineProgressBar.style.width = progressPercent.toFixed(2) + "%";
    if (onlineProgress) {
      onlineProgress.setAttribute("aria-valuenow", String(elapsedSeconds));
      onlineProgress.setAttribute(
        "aria-valuetext",
        i18n.t(
          "online.progressText",
          "Đã chờ {elapsed} trên tối đa {max} giây",
          { elapsed: elapsedSeconds, max: 30 }
        )
      );
    }
    var readyCount = 0;
    onlinePlayerSlots.forEach(function (slot, index) {
      var player = onlinePlayers[index] || {
        name: "---", ping: 0, ready: false, local: false
      };
      if (player.ready) readyCount += 1;
      renderOnlinePlayer(slot, player);
    });
    if (onlinePlayerCount) onlinePlayerCount.textContent = readyCount + "/4";
    if (onlineRetryButton) {
      var searching = onlineMode === "searching";
      onlineRetryButton.disabled = searching;
      onlineRetryButton.setAttribute("aria-disabled", String(searching));
    }
    if (onlineBackLabel) {
      onlineBackLabel.textContent = i18n.t(
        onlineMode === "searching" ? "online.cancel" : "online.back",
        onlineMode === "searching" ? "HỦY" : "QUAY LẠI"
      );
    }
  }

  function finishOnlineSearch(runId) {
    if (runId !== onlineSearchRunId || onlineMode !== "searching") return;
    clearOnlineSearchTimers(false);
    onlineMode = "matched";
    renderOnline(true);
    menuAudio.play("notify");
    if (onlineRetryButton && isOnlineOpen()) onlineRetryButton.focus();
  }

  function startOnlineSearch(playCue) {
    if (!isOnlineOpen()) return false;
    clearOnlineSearchTimers(true);
    var runId = onlineSearchRunId;
    onlineMode = "searching";
    onlineSearchDuration = Math.min(
      30000,
      5000 + Math.floor(Math.random() * 25001)
    );
    onlineSearchStartedAt = Date.now();
    onlineSearchDeadline = onlineSearchStartedAt + onlineSearchDuration;
    onlineLastRenderedSecond = -1;
    createOnlinePlayers();
    renderOnline(true);
    if (playCue) menuAudio.play("transition");
    onlineSearchInterval = window.setInterval(function () {
      if (runId !== onlineSearchRunId || onlineMode !== "searching") return;
      if (Date.now() >= onlineSearchDeadline) {
        finishOnlineSearch(runId);
        return;
      }
      renderOnline(false);
    }, 500);
    onlineSearchTimeout = window.setTimeout(function () {
      finishOnlineSearch(runId);
    }, onlineSearchDuration);
    return true;
  }

  function cancelOnlineSearch() {
    if (onlineMode !== "searching") return false;
    clearOnlineSearchTimers(true);
    onlineMode = "cancelled";
    onlineSearchDuration = 0;
    onlineSearchStartedAt = 0;
    onlineSearchDeadline = 0;
    createOnlinePlayers();
    renderOnline(true);
    menuAudio.play("back");
    if (onlineRetryButton && isOnlineOpen()) onlineRetryButton.focus();
    return true;
  }

  function openOnline(opener) {
    if (
      !onlineScreen ||
      !onlineScreen.hidden ||
      isSettingsOpen() ||
      isGuestOpen() ||
      isMissionOpen()
    ) return false;
    onlineFocusBeforeOpen = opener && document.contains(opener)
      ? opener
      : document.activeElement;
    onlineScreen.hidden = false;
    onlineScreen.setAttribute("aria-hidden", "false");
    document.documentElement.classList.add("is-online-open");
    setMainMenuAccessibility(true);
    startOnlineSearch(false);
    window.setTimeout(function () {
      if (onlineBackButton) onlineBackButton.focus();
      else if (onlinePanel) onlinePanel.focus();
    }, 0);
    return true;
  }

  function closeOnline() {
    if (!isOnlineOpen() || menuBusy) return false;
    clearOnlineSearchTimers(true);
    onlineMode = "closed";
    onlineScreen.hidden = true;
    onlineScreen.setAttribute("aria-hidden", "true");
    document.documentElement.classList.remove("is-online-open");
    setMainMenuAccessibility(false);
    var focusTarget = onlineFocusBeforeOpen && document.contains(onlineFocusBeforeOpen)
      ? onlineFocusBeforeOpen
      : onlineMenuButton;
    onlineFocusBeforeOpen = null;
    if (focusTarget && !focusTarget.disabled) focusTarget.focus();
    return true;
  }

  function setMissionHelp(message, warning) {
    if (!missionHelp) return;
    window.clearTimeout(missionHelpTimer);
    var copy = missionHelp.querySelector("span");
    if (copy) copy.textContent = message;
    missionHelp.classList.toggle("is-warning", Boolean(warning));
    if (warning) {
      missionHelpTimer = window.setTimeout(function () {
        missionHelp.classList.remove("is-warning");
        renderMissions();
      }, 1800);
    }
  }

  function flashMissionControl(control, denied) {
    if (!control) return;
    control.classList.remove("is-tap-flash", "is-denied");
    void control.offsetWidth;
    control.classList.add(denied ? "is-denied" : "is-tap-flash");
    window.setTimeout(function () {
      control.classList.remove("is-tap-flash", "is-denied");
    }, denied ? 320 : 240);
  }

  function handleBack() {
    if (menuBusy || missionPlayPending || guestRequestPending) return true;
    if (isGuestOpen()) {
      if (guestNameInput && document.activeElement === guestNameInput) {
        guestNameInput.blur();
        requestLayout();
        return true;
      }
      menuAudio.play("back");
      return closeGuestScreen();
    }
    if (isSettingsOpen()) {
      menuAudio.play("back");
      return closeSettings(false);
    }
    if (isOnlineOpen()) {
      if (onlineMode === "searching") return cancelOnlineSearch();
      menuAudio.play("back");
      return closeOnline();
    }
    if (!isMissionOpen()) return false;
    menuAudio.play("back");
    return closeMissions();
  }

  function flashSettingsControl(control) {
    if (!control || control.disabled) return;
    control.classList.remove("settings-control-tap");
    void control.offsetWidth;
    control.classList.add("settings-control-tap");
    window.setTimeout(function () {
      control.classList.remove("settings-control-tap");
    }, 220);
  }

  function updateLocalSetting(control) {
    var setting = control.getAttribute("data-setting");
    var value = control.getAttribute("data-value");
    var graphics = state.graphics;

    if (setting === "quality") {
      applyLocalQualityPreset(value);
      return { action: "settings-quality", payload: { value: value } };
    }
    if (setting === "aa") {
      graphics.antiAliasing = value;
      return { action: "settings-aa", payload: { value: value } };
    }
    if (setting === "dof") {
      graphics.depthOfField = value;
      return { action: "settings-dof", payload: { value: value } };
    }
    if (setting === "fbs") {
      graphics.fbsEnabled = !graphics.fbsEnabled;
      return {
        action: "settings-fbs",
        payload: { value: graphics.fbsEnabled ? "on" : "off" }
      };
    }
    if (setting === "ssao") {
      graphics.ssaoEnabled = !graphics.ssaoEnabled;
      return {
        action: "settings-ssao",
        payload: { value: graphics.ssaoEnabled ? "on" : "off" }
      };
    }
    return null;
  }

  settingsControls.forEach(function (control) {
    control.addEventListener("click", function () {
      if (control.disabled) return;
      var update = updateLocalSetting(control);
      if (!update) return;
      flashSettingsControl(control);
      menuAudio.play("activate");
      renderSettings();
      sendToHost(update.action, update.payload);
    });

    control.addEventListener("keydown", function (event) {
      if (control.disabled || control.getAttribute("role") !== "radio") return;
      var direction = 0;
      if (event.key === "ArrowLeft" || event.key === "ArrowUp") direction = -1;
      if (event.key === "ArrowRight" || event.key === "ArrowDown") direction = 1;
      if (!direction) return;
      event.preventDefault();
      var group = control.parentNode;
      var controls = Array.prototype.slice.call(group.querySelectorAll("button:not(:disabled)"));
      var index = controls.indexOf(control);
      if (index < 0 || controls.length < 2) return;
      var next = controls[(index + direction + controls.length) % controls.length];
      next.focus();
      next.click();
    });
  });

  settingsTabs.forEach(function (tab, tabIndex) {
    tab.addEventListener("click", function () {
      var view = tab.getAttribute("data-settings-tab");
      if (view === settingsView) return;
      setSettingsView(view, false);
      menuAudio.play("panel");
    });
    tab.addEventListener("keydown", function (event) {
      var direction = 0;
      if (event.key === "ArrowLeft" || event.key === "ArrowUp" ||
          event.keyCode === 37 || event.keyCode === 38) direction = -1;
      if (event.key === "ArrowRight" || event.key === "ArrowDown" ||
          event.keyCode === 39 || event.keyCode === 40) direction = 1;
      var nextIndex = -1;
      if (direction) {
        nextIndex = (tabIndex + direction + settingsTabs.length) % settingsTabs.length;
      } else if (event.key === "Home" || event.keyCode === 36) {
        nextIndex = 0;
      } else if (event.key === "End" || event.keyCode === 35) {
        nextIndex = settingsTabs.length - 1;
      }
      if (nextIndex < 0) return;
      event.preventDefault();
      var nextTab = settingsTabs[nextIndex];
      setSettingsView(nextTab.getAttribute("data-settings-tab"), true);
      menuAudio.play("panel");
    });
  });

  if (settingsAudioMaster) {
    settingsAudioMaster.addEventListener("click", function () {
      state.audio.enabled = !state.audio.enabled;
      state.audioEnabled = state.audio.enabled;
      menuAudio.setEnabled(state.audio.enabled, true);
      renderAudioSettings();
      updateAudioToggle();
      if (state.audio.enabled) menuAudio.play("activate");
      sendToHost("settings-audio-enabled", {
        value: state.audio.enabled ? "on" : "off"
      });
    });
  }

  settingsAudioRanges.forEach(function (range) {
    var channel = range.getAttribute("data-audio-range");
    function previewRange(persist) {
      state.audio[channel] = clampAudioPercentage(range.value, state.audio[channel]);
      menuAudio.setMix(state.audio, persist);
      renderAudioSettings();
    }
    range.addEventListener("input", function () { previewRange(false); });
    range.addEventListener("change", function () {
      previewRange(true);
      sendToHost("settings-audio-" + channel, {
        value: String(state.audio[channel])
      });
      if (channel === "sfx") menuAudio.play("activate");
    });
  });

  settingsLanguageButtons.forEach(function (button, buttonIndex) {
    function chooseLanguage() {
      var language = i18n.normalize(button.getAttribute("data-language"));
      if (language === state.language) return;
      state.language = language;
      i18n.apply(language);
      renderSettings();
      renderMissions();
      renderGuest();
      renderOnline(true);
      updateAudioToggle();
      menuAudio.play("activate");
      sendToHost("settings-language", { value: language });
    }
    button.addEventListener("click", chooseLanguage);
    button.addEventListener("keydown", function (event) {
      var nextIndex = -1;
      var columns = 4;
      var rowStart = Math.floor(buttonIndex / columns) * columns;
      var rowEnd = Math.min(rowStart + columns, settingsLanguageButtons.length) - 1;
      if (event.key === "ArrowLeft" || event.keyCode === 37) {
        nextIndex = buttonIndex === rowStart ? rowEnd : buttonIndex - 1;
      } else if (event.key === "ArrowRight" || event.keyCode === 39) {
        nextIndex = buttonIndex === rowEnd ? rowStart : buttonIndex + 1;
      } else if (event.key === "ArrowUp" || event.keyCode === 38) {
        nextIndex = buttonIndex - columns;
        if (nextIndex < 0) {
          nextIndex = (Math.ceil(settingsLanguageButtons.length / columns) - 1) *
            columns + (buttonIndex % columns);
          if (nextIndex >= settingsLanguageButtons.length) nextIndex -= columns;
        }
      } else if (event.key === "ArrowDown" || event.keyCode === 40) {
        nextIndex = buttonIndex + columns;
        if (nextIndex >= settingsLanguageButtons.length) {
          nextIndex = buttonIndex % columns;
        }
      } else if (event.key === "Home" || event.keyCode === 36) {
        nextIndex = 0;
      } else if (event.key === "End" || event.keyCode === 35) {
        nextIndex = settingsLanguageButtons.length - 1;
      }
      if (nextIndex < 0) return;
      event.preventDefault();
      var nextButton = settingsLanguageButtons[nextIndex];
      nextButton.focus();
      nextButton.click();
    });
  });

  settingsCloseButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      var complete = button.hasAttribute("data-settings-complete");
      flashSettingsControl(button);
      menuAudio.play(complete ? "activate" : "back");
      closeSettings(complete);
    });
  });

  if (settingsPanel) {
    settingsPanel.addEventListener("keydown", function (event) {
      if (event.key === "Escape" || event.key === "Esc" || event.keyCode === 27) {
        event.preventDefault();
        handleBack();
        return;
      }
      if (event.key !== "Tab" && event.keyCode !== 9) return;
      var focusable = Array.prototype.slice.call(
        settingsPanel.querySelectorAll("button:not(:disabled), input:not(:disabled)")
      ).filter(function (control) {
        return control.offsetWidth > 0 && control.offsetHeight > 0;
      });
      if (!focusable.length) return;
      var first = focusable[0];
      var last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    });
  }

  function flashGuestControl(control, denied) {
    if (!control || control.disabled) return;
    control.classList.remove("is-tap-flash");
    void control.offsetWidth;
    control.classList.add("is-tap-flash");
    window.setTimeout(function () {
      control.classList.remove("is-tap-flash");
    }, denied ? 320 : 240);
  }

  function normalizeGuestName(value) {
    var normalized = String(value || "").replace(/\s+/g, " ").trim();
    if (typeof normalized.normalize === "function") {
      try {
        normalized = normalized.normalize("NFC");
      } catch (error) {
        // Keep the original text and let Unity perform authoritative validation.
      }
    }
    if (normalized.length < 2 || normalized.length > 20) {
      return {
        valid: false,
        value: normalized,
        message: i18n.t("guest.error.length", "Tên cần có từ 2 đến 20 ký tự.")
      };
    }
    var hasLetterOrDigit = false;
    for (var index = 0; index < normalized.length; index += 1) {
      var code = normalized.charCodeAt(index);
      var character = normalized.charAt(index);
      var asciiLetterOrDigit =
        (code >= 48 && code <= 57) ||
        (code >= 65 && code <= 90) ||
        (code >= 97 && code <= 122);
      var latinLetter =
        ((code >= 0x00c0 && code <= 0x02af) ||
          (code >= 0x1e00 && code <= 0x1eff)) &&
        code !== 0x00d7 &&
        code !== 0x00f7;
      var cyrillicLetter = code >= 0x0400 && code <= 0x052f;
      var cjkLetter =
        (code >= 0x3400 && code <= 0x4dbf) ||
        (code >= 0x4e00 && code <= 0x9fff);
      var eastAsianLetter =
        (code >= 0x3040 && code <= 0x30ff) ||
        (code >= 0xac00 && code <= 0xd7af);
      var combiningMark =
        (code >= 0x0300 && code <= 0x036f) ||
        (code >= 0x1ab0 && code <= 0x1aff) ||
        (code >= 0x1dc0 && code <= 0x1dff) ||
        (code >= 0x20d0 && code <= 0x20ff) ||
        (code >= 0xfe20 && code <= 0xfe2f);
      var separator = character === " " || character === "_" || character === "-";
      var supportedLetter = latinLetter || cyrillicLetter || cjkLetter || eastAsianLetter;
      if (asciiLetterOrDigit || supportedLetter) hasLetterOrDigit = true;
      if (
        code < 32 ||
        code === 127 ||
        code === 0x200b ||
        code === 0x200c ||
        code === 0x200d ||
        code === 0xfeff ||
        (code >= 0xd800 && code <= 0xdfff) ||
        (!asciiLetterOrDigit && !supportedLetter && !combiningMark && !separator)
      ) {
        return {
          valid: false,
          value: normalized,
          message: i18n.t(
            "guest.error.unsupported",
            "Tên chứa ký tự không được hỗ trợ."
          )
        };
      }
    }
    if (!hasLetterOrDigit) {
      return {
        valid: false,
        value: normalized,
        message: i18n.t(
          "guest.error.required",
          "Tên cần có ít nhất một chữ cái hoặc chữ số."
        )
      };
    }
    return { valid: true, value: normalized, message: "" };
  }

  function beginGuestRequest(action, payload) {
    if (guestRequestPending || menuBusy) return false;
    guestRequestSequence += 1;
    guestPendingRequestId =
      Date.now().toString(36) + "-" + guestRequestSequence.toString(36);
    payload = payload || {};
    payload.requestId = guestPendingRequestId;
    guestRequestPending = action;
    if (guestPanel) {
      guestPanel.setAttribute("aria-busy", "true");
      guestPanel.focus();
    }
    renderGuest();
    if (!sendToHost(action, payload)) {
      guestRequestPending = "";
      guestPendingRequestId = "";
      if (guestPanel) guestPanel.setAttribute("aria-busy", "false");
      renderGuest();
      setGuestFeedback(
        i18n.t(
          "guest.response.unityUnavailable",
          "Không kết nối được Unity để lưu hồ sơ."
        ),
        true
      );
      window.setTimeout(function () {
        if (!isGuestOpen()) return;
        if (action === "guest-create" && guestNameInput) guestNameInput.focus();
        else {
          var activeMode = guestScreen.querySelector(".guest-mode.is-active");
          if (activeMode) activeMode.focus();
        }
      }, 0);
      return false;
    }
    window.clearTimeout(guestRequestTimer);
    guestRequestTimer = window.setTimeout(function () {
      if (!guestRequestPending) return;
      guestRequestPending = "";
      guestPendingRequestId = "";
      if (guestPanel) guestPanel.setAttribute("aria-busy", "false");
      renderGuest();
      setGuestFeedback(
        i18n.t("guest.response.timeout", "Unity chưa phản hồi. Vui lòng thử lại."),
        true
      );
      window.setTimeout(function () {
        if (!isGuestOpen()) return;
        if (action === "guest-create" && guestNameInput) guestNameInput.focus();
        else {
          var activeMode = guestScreen.querySelector(".guest-mode.is-active");
          if (activeMode) activeMode.focus();
        }
      }, 0);
    }, 2200);
    return true;
  }

  function chooseGuestMode(mode, sourceButton) {
    if (mode !== "create" && mode !== "login") return false;
    if (mode === "login" && !state.guest.exists) {
      flashGuestControl(sourceButton, true);
      menuAudio.play("denied");
      setGuestFeedback(
        i18n.t(
          "guest.response.createFirst",
          "Chưa có hồ sơ khách trên thiết bị. Hãy tạo hồ sơ trước."
        ),
        true
      );
      window.setTimeout(function () {
        var createModeButton = guestScreen.querySelector('[data-guest-mode="create"]');
        if (createModeButton) createModeButton.focus();
      }, 0);
      return false;
    }
    guestMode = mode;
    setGuestFeedback("", false);
    renderGuest();
    return true;
  }

  guestModeButtons.forEach(function (button, buttonIndex) {
    button.addEventListener("click", function () {
      if (guestRequestPending || menuBusy) return;
      var mode = button.getAttribute("data-guest-mode");
      if (!chooseGuestMode(mode, button)) return;
      flashGuestControl(button, false);
      menuAudio.play("panel");
    });

    button.addEventListener("keydown", function (event) {
      var key = event.key;
      var keyCode = event.keyCode;
      var direction = 0;
      if (key === "ArrowLeft" || key === "ArrowUp" || keyCode === 37 || keyCode === 38) {
        direction = -1;
      } else if (
        key === "ArrowRight" || key === "ArrowDown" || keyCode === 39 || keyCode === 40
      ) {
        direction = 1;
      }
      var targetIndex = -1;
      if (direction) {
        targetIndex = (buttonIndex + direction + guestModeButtons.length) % guestModeButtons.length;
      } else if (key === "Home" || keyCode === 36) {
        targetIndex = 0;
      } else if (key === "End" || keyCode === 35) {
        targetIndex = guestModeButtons.length - 1;
      }
      if (targetIndex < 0) return;
      event.preventDefault();
      guestModeButtons[targetIndex].focus();
      guestModeButtons[targetIndex].click();
    });
  });

  guestAvatarButtons.forEach(function (button, buttonIndex) {
    button.addEventListener("click", function () {
      if (button.disabled || guestRequestPending || guestMode !== "create") return;
      guestDraftAvatar = button.getAttribute("data-guest-avatar");
      renderGuest();
      flashGuestControl(button, false);
      menuAudio.play("activate");
    });

    button.addEventListener("keydown", function (event) {
      var key = event.key;
      var keyCode = event.keyCode;
      var nextIndex = -1;
      if (key === "ArrowLeft" || key === "ArrowUp" || keyCode === 37 || keyCode === 38) {
        nextIndex = (buttonIndex + guestAvatarButtons.length - 1) % guestAvatarButtons.length;
      } else if (
        key === "ArrowRight" || key === "ArrowDown" || keyCode === 39 || keyCode === 40
      ) {
        nextIndex = (buttonIndex + 1) % guestAvatarButtons.length;
      } else if (key === "Home" || keyCode === 36) {
        nextIndex = 0;
      } else if (key === "End" || keyCode === 35) {
        nextIndex = guestAvatarButtons.length - 1;
      }
      if (nextIndex < 0) return;
      event.preventDefault();
      guestAvatarButtons[nextIndex].focus();
      guestAvatarButtons[nextIndex].click();
    });
  });

  if (guestNameInput) {
    guestNameInput.addEventListener("focus", function () {
      if (guestNameInput.parentNode) {
        guestNameInput.parentNode.classList.add("is-focused");
      }
      guestKeyboardScale = Math.max(currentLayoutScale, 0.001);
      guestKeyboardViewportWidth = window.visualViewport
        ? window.visualViewport.width
        : window.innerWidth;
      requestLayout();
    });
    guestNameInput.addEventListener("input", function () {
      guestNameInput.setAttribute("aria-invalid", "false");
      setGuestFeedback("", false);
    });
    guestNameInput.addEventListener("keydown", function (event) {
      if (
        event.isComposing ||
        event.keyCode === 229 ||
        (event.key !== "Enter" && event.keyCode !== 13)
      ) return;
      event.preventDefault();
      if (guestCreateButton && !guestCreateButton.disabled) guestCreateButton.click();
    });
    guestNameInput.addEventListener("blur", function () {
      if (guestNameInput.parentNode) {
        guestNameInput.parentNode.classList.remove("is-focused");
      }
      guestKeyboardScale = 0;
      guestKeyboardViewportWidth = 0;
      document.documentElement.classList.remove("is-guest-keyboard-open");
      requestLayout();
    }, { passive: true });
  }

  if (guestCreateButton) {
    guestCreateButton.addEventListener("click", function () {
      if (guestCreateButton.disabled || guestMode !== "create") return;
      var validation = normalizeGuestName(guestNameInput ? guestNameInput.value : "");
      if (!validation.valid) {
        if (guestNameInput) {
          guestNameInput.value = validation.value;
          guestNameInput.setAttribute("aria-invalid", "true");
          guestNameInput.focus();
        }
        setGuestFeedback(validation.message, true);
        menuAudio.play("error");
        return;
      }
      if (guestNameInput) {
        guestNameInput.value = validation.value;
        guestNameInput.setAttribute("aria-invalid", "false");
      }
      setGuestFeedback(
        i18n.t("guest.response.saving", "Đang lưu hồ sơ khách..."),
        false
      );
      flashGuestControl(guestCreateButton, false);
      menuAudio.play("activate");
      beginGuestRequest("guest-create", {
        displayName: validation.value,
        avatar: guestDraftAvatar
      });
    });
  }

  if (guestLoginButton) {
    guestLoginButton.addEventListener("click", function () {
      if (guestLoginButton.disabled || !state.guest.exists) return;
      flashGuestControl(guestLoginButton, false);
      menuAudio.play("activate");
      setGuestFeedback(
        i18n.t(
          "guest.response.opening",
          "Đang mở hồ sơ khách trên thiết bị..."
        ),
        false
      );
      beginGuestRequest("guest-login", { id: state.guest.id });
    });
  }

  if (guestBackButton) {
    guestBackButton.addEventListener("click", function () {
      if (guestRequestPending || menuBusy) return;
      flashGuestControl(guestBackButton, false);
      menuAudio.play("back");
      closeGuestScreen();
    });
  }

  if (guestScreen) {
    guestScreen.addEventListener("keydown", function (event) {
      if (event.key === "Escape" || event.key === "Esc" || event.keyCode === 27) {
        event.preventDefault();
        if (!guestRequestPending) {
          menuAudio.play("back");
          closeGuestScreen();
        }
        return;
      }
      if (event.key !== "Tab" && event.keyCode !== 9) return;
      var focusable = Array.prototype.slice.call(
        guestScreen.querySelectorAll("button:not(:disabled), input:not(:disabled)")
      ).filter(function (control) {
        return control.offsetWidth > 0 && control.offsetHeight > 0 && control.tabIndex >= 0;
      });
      if (!focusable.length) return;
      var first = focusable[0];
      var last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    });
  }

  function missionPointerPoint(event) {
    var points = event.touches && event.touches.length
      ? event.touches
      : event.changedTouches;
    var point = points && points.length ? points[0] : event;
    return {
      x: Number(point.clientX) || 0,
      y: Number(point.clientY) || 0
    };
  }

  function beginMissionSwipe(event) {
    if (!missionGrid) return;
    var point = missionPointerPoint(event);
    missionScrollTouched = true;
    missionSwipeGesture = false;
    missionPointerTracking = true;
    missionPointerStartX = point.x;
    missionPointerStartY = point.y;
    missionPointerStartScrollLeft = missionGrid.scrollLeft;
  }

  function updateMissionSwipe(event) {
    if (!missionGrid || !missionPointerTracking) return;
    var point = missionPointerPoint(event);
    if (
      Math.abs(point.x - missionPointerStartX) > 10 ||
      Math.abs(point.y - missionPointerStartY) > 10 ||
      Math.abs(missionGrid.scrollLeft - missionPointerStartScrollLeft) > 10
    ) {
      missionSwipeGesture = true;
    }
  }

  function finishMissionSwipe(event) {
    if (!missionPointerTracking) return;
    updateMissionSwipe(event);
    missionPointerTracking = false;
    if (missionSwipeGesture) missionSwipeClickUntil = Date.now() + 420;
  }

  if (missionGrid) {
    if (window.PointerEvent) {
      missionGrid.addEventListener("pointerdown", beginMissionSwipe, { passive: true });
      missionGrid.addEventListener("pointermove", updateMissionSwipe, { passive: true });
      missionGrid.addEventListener("pointerup", finishMissionSwipe, { passive: true });
      missionGrid.addEventListener("pointercancel", finishMissionSwipe, { passive: true });
    } else {
      missionGrid.addEventListener("touchstart", beginMissionSwipe, { passive: true });
      missionGrid.addEventListener("touchmove", updateMissionSwipe, { passive: true });
      missionGrid.addEventListener("touchend", finishMissionSwipe, { passive: true });
      missionGrid.addEventListener("touchcancel", finishMissionSwipe, { passive: true });
      missionGrid.addEventListener("mousedown", beginMissionSwipe, { passive: true });
      document.addEventListener("mousemove", updateMissionSwipe, { passive: true });
      document.addEventListener("mouseup", finishMissionSwipe, { passive: true });
    }
    missionGrid.addEventListener("wheel", function (event) {
      missionScrollTouched = true;
      var delta = Math.abs(event.deltaY) > Math.abs(event.deltaX)
        ? event.deltaY
        : event.deltaX;
      if (!delta) return;
      missionGrid.scrollLeft += delta;
      event.preventDefault();
    }, { passive: false });
  }

  missionCards.forEach(function (card, cardIndex) {
    function recordMissionPointer() {
      lastPointerInputAt = Date.now();
    }

    if (window.PointerEvent) {
      card.addEventListener("pointerdown", recordMissionPointer, { passive: true });
    } else {
      card.addEventListener("touchstart", recordMissionPointer, { passive: true });
      card.addEventListener("mousedown", recordMissionPointer, { passive: true });
    }

    card.addEventListener("click", function (event) {
      if (Date.now() < missionSwipeClickUntil) {
        event.preventDefault();
        event.stopPropagation();
        return;
      }
      if (menuBusy || missionPlayPending) return;
      var missionId = card.getAttribute("data-mission-card");
      var item = findMissionById(missionId);
      if (!item) return;
      if (!item.unlocked) {
        flashMissionControl(card, true);
        menuAudio.play("denied");
        var lockedDisplay = i18n.localizeMission(item);
        setMissionHelp(
          i18n.t(
            "mission.unlockTarget",
            "Hoàn thành màn trước để mở khóa {title}",
            { title: lockedDisplay.title }
          ),
          true
        );
        return;
      }
      state.missions.selectedId = item.id;
      if (missionHelp) missionHelp.classList.remove("is-warning");
      renderMissions();
      flashMissionControl(card, false);
      menuAudio.play("activate");
      sendToHost("mission-select", { value: item.id });
    });

    card.addEventListener("focus", function () {
      if (Date.now() - lastPointerInputAt > 180) menuAudio.play("hover");
    });

    card.addEventListener("keydown", function (event) {
      var key = event.key;
      var keyCode = event.keyCode;
      var nextIndex = -1;
      if (
        key === "ArrowLeft" || keyCode === 37 ||
        key === "ArrowUp" || keyCode === 38
      ) {
        nextIndex = (cardIndex + missionCards.length - 1) % missionCards.length;
      } else if (
        key === "ArrowRight" || keyCode === 39 ||
        key === "ArrowDown" || keyCode === 40
      ) {
        nextIndex = (cardIndex + 1) % missionCards.length;
      } else if (key === "Home" || keyCode === 36) {
        nextIndex = 0;
      } else if (key === "End" || keyCode === 35) {
        nextIndex = missionCards.length - 1;
      }
      if (nextIndex < 0) return;
      event.preventDefault();
      var nextCard = missionCards[nextIndex];
      nextCard.focus();
      missionScrollTouched = true;
      centerMissionCard(nextCard);
      var nextItem = findMissionById(nextCard.getAttribute("data-mission-card"));
      if (nextItem && nextItem.unlocked) {
        nextCard.click();
      } else if (nextItem) {
        var nextDisplay = i18n.localizeMission(nextItem);
        setMissionHelp(
          i18n.t(
            "mission.unlockTarget",
            "Hoàn thành màn trước để mở khóa {title}",
            { title: nextDisplay.title }
          ),
          true
        );
      }
    });
  });

  if (missionBackButton) {
    missionBackButton.addEventListener("click", function () {
      if (menuBusy || missionPlayPending) return;
      flashMissionControl(missionBackButton, false);
      menuAudio.play("back");
      closeMissions();
    });
  }

  if (missionPlayButton) {
    missionPlayButton.addEventListener("click", function () {
      if (menuBusy || missionPlayPending || missionPlayButton.disabled) return;
      var selected = findMissionById(state.missions.selectedId);
      if (!selected || !selected.unlocked) {
        menuAudio.play("denied");
        setMissionHelp(
          i18n.t("runtime.mission.locked", "Màn chơi này chưa được mở khóa"),
          true
        );
        return;
      }
      flashMissionControl(missionPlayButton, false);
      missionPlayPending = true;
      renderMissions();
      var delivered = sendToHost("mission-play", { value: selected.id });
      if (!delivered) {
        missionPlayPending = false;
        renderMissions();
        return;
      }
      window.clearTimeout(missionPlayTimer);
      missionPlayTimer = window.setTimeout(function () {
        if (!menuBusy) {
          missionPlayPending = false;
          renderMissions();
        }
      }, 1600);
    });
  }

  if (missionPanel) {
    missionPanel.addEventListener("keydown", function (event) {
      if (event.key === "Escape" || event.key === "Esc" || event.keyCode === 27) {
        event.preventDefault();
        handleBack();
        return;
      }
      if (event.key !== "Tab" && event.keyCode !== 9) return;
      var focusable = Array.prototype.slice.call(
        missionPanel.querySelectorAll("button:not(:disabled)")
      ).filter(function (button) {
        return button.offsetWidth > 0 && button.offsetHeight > 0 && button.tabIndex >= 0;
      });
      if (!focusable.length) return;
      var first = focusable[0];
      var last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    });
  }

  function flashOnlineControl(control) {
    if (!control || control.disabled) return;
    control.classList.remove("is-tap-flash");
    void control.offsetWidth;
    control.classList.add("is-tap-flash");
    window.setTimeout(function () {
      control.classList.remove("is-tap-flash");
    }, 240);
  }

  if (onlineBackButton) {
    onlineBackButton.addEventListener("click", function () {
      flashOnlineControl(onlineBackButton);
      if (onlineMode === "searching") {
        cancelOnlineSearch();
      } else {
        menuAudio.play("back");
        closeOnline();
      }
    });
  }

  if (onlineRetryButton) {
    onlineRetryButton.addEventListener("click", function () {
      flashOnlineControl(onlineRetryButton);
      startOnlineSearch(true);
    });
  }

  if (onlinePanel) {
    onlinePanel.addEventListener("keydown", function (event) {
      if (event.key === "Escape" || event.key === "Esc" || event.keyCode === 27) {
        event.preventDefault();
        handleBack();
        return;
      }
      if (event.key !== "Tab" && event.keyCode !== 9) return;
      var focusable = Array.prototype.slice.call(
        onlinePanel.querySelectorAll("button:not(:disabled)")
      ).filter(function (button) {
        return button.offsetWidth > 0 && button.offsetHeight > 0;
      });
      if (!focusable.length) return;
      var first = focusable[0];
      var last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    });
  }

  actionButtons.forEach(function (button) {
    var tapFlashTimer = 0;

    function flashButton() {
      if (button.disabled) return;
      window.clearTimeout(tapFlashTimer);
      button.classList.remove("is-tap-flash");
      // Restart the short pulse even when two valid taps happen close together.
      void button.offsetWidth;
      button.classList.add("is-tap-flash");
      tapFlashTimer = window.setTimeout(function () {
        button.classList.remove("is-tap-flash");
      }, 240);
    }

    function pressButton() {
      if (!button.disabled) {
        userInteracted = true;
        lastPointerInputAt = Date.now();
        button.classList.add("is-pressed");
      }
    }

    function releaseButton() {
      button.classList.remove("is-pressed");
    }

    function hoverButton() {
      if (!button.disabled) menuAudio.play("hover");
    }

    if (window.PointerEvent) {
      button.addEventListener("pointerdown", pressButton, { passive: true });
      button.addEventListener("pointerup", releaseButton, { passive: true });
      button.addEventListener("pointercancel", releaseButton, { passive: true });
      button.addEventListener("pointerleave", releaseButton, { passive: true });
      button.addEventListener("pointerenter", function (event) {
        if (event.pointerType === "mouse") hoverButton();
      }, { passive: true });
    } else {
      button.addEventListener("touchstart", pressButton, { passive: true });
      button.addEventListener("touchend", releaseButton, { passive: true });
      button.addEventListener("touchcancel", releaseButton, { passive: true });
      button.addEventListener("mousedown", pressButton, { passive: true });
      button.addEventListener("mouseup", releaseButton, { passive: true });
      button.addEventListener("mouseleave", releaseButton, { passive: true });
      button.addEventListener("mouseenter", hoverButton, { passive: true });
    }

    button.addEventListener("blur", function () {
      button.classList.remove("is-pressed");
    });

    button.addEventListener("focus", function () {
      if (button.classList.contains("tech-button")) selectButton(button);
      if (Date.now() - lastPointerInputAt > 180) menuAudio.play("hover");
    });

    button.addEventListener("click", function () {
      if (button.disabled) return;
      userInteracted = true;
      var action = button.getAttribute("data-action");
      if (action === "audio-toggle") {
        var now = Date.now();
        if (now < audioToggleReadyAt) return;
        audioToggleReadyAt = now + 400;
        flashButton();
        state.audioEnabled = !state.audioEnabled;
        state.audio.enabled = state.audioEnabled;
        menuAudio.setEnabled(state.audioEnabled, true);
        updateAudioToggle();
        renderAudioSettings();
        if (state.audioEnabled) menuAudio.play("activate");
        sendToHost(action, { enabled: state.audioEnabled });
        return;
      }
      flashButton();
      selectButton(button);
      playActionSound(action);
      if (action === "online-open") {
        openOnline(button);
        return;
      }
      if (action === "settings") openSettings();
      if (action === "new-game" || action === "mission-open") openMissions(button);
      if (action === "profile") openGuestScreen();
      sendToHost(action, {});
    });
  });

  window.addEventListener("resize", requestLayout, { passive: true });
  window.addEventListener("orientationchange", requestLayout, { passive: true });
  document.addEventListener("visibilitychange", function () {
    if (!isOnlineOpen() || onlineMode !== "searching") return;
    if (Date.now() >= onlineSearchDeadline) finishOnlineSearch(onlineSearchRunId);
    else renderOnline(true);
  }, { passive: true });
  window.addEventListener("contextmenu", function (event) {
    var target = event.target;
    if (target && (target.tagName === "INPUT" || target.tagName === "TEXTAREA")) return;
    event.preventDefault();
  });
  if (window.visualViewport) {
    window.visualViewport.addEventListener("resize", requestLayout, { passive: true });
  }

  window.FranklinMenu = Object.freeze({
    setState: setState,
    setBusy: setBusy,
    setHostMetrics: setHostMetrics,
    notify: showToast,
    playSound: function (cue) { return menuAudio.play(cue); },
    setAudioSuspended: function (suspended) { menuAudio.setSuspended(suspended); },
    getState: function () { return Object.assign({}, state); },
    dispatch: sendToHost,
    handleBack: handleBack,
    isSettingsOpen: isSettingsOpen,
    isMissionOpen: isMissionOpen,
    isOnlineOpen: isOnlineOpen,
    isGuestOpen: isGuestOpen,
    refreshLayout: requestLayout
  });

  setState(state);
  updateLayout();
  sendToHost("ready", { viewport: { width: window.innerWidth, height: window.innerHeight } });
}());

(function () {
  "use strict";

  var dictionaries = {
    vi: {
      "main.profile": "HỒ SƠ", "main.sync": "ĐỒNG BỘ", "main.continue": "TIẾP TỤC",
      "main.newGame": "TRÒ CHƠI MỚI", "main.missions": "NHIỆM VỤ", "main.settings": "CÀI ĐẶT",
      "main.quit": "THOÁT", "main.audio": "ÂM THANH", "common.on": "BẬT", "common.off": "TẮT",
      "settings.title": "CÀI ĐẶT", "settings.description": "Tùy chỉnh đồ họa, âm thanh và ngôn ngữ.",
      "settings.tabs.graphics": "ĐỒ HỌA", "settings.tabs.audio": "ÂM THANH", "settings.tabs.language": "NGÔN NGỮ",
      "settings.graphics.quality": "CHẤT LƯỢNG", "settings.graphics.qualityHint": "Tự tối ưu theo thiết bị",
      "settings.graphics.low": "THẤP", "settings.graphics.balanced": "VỪA", "settings.graphics.high": "CAO",
      "settings.graphics.aa": "KHỬ RĂNG CƯA", "settings.graphics.dof": "LẤY NÉT DOF",
      "settings.graphics.near": "GẦN", "settings.graphics.far": "XA", "settings.graphics.fbs": "BÓNG NHÂN VẬT",
      "settings.graphics.fbsHint": "Bóng mềm, chi phí thấp", "settings.graphics.ssao": "ĐỔ BÓNG MÔI TRƯỜNG",
      "settings.graphics.ssaoHint": "Tăng chiều sâu khung cảnh", "settings.autosave": "THAY ĐỔI ĐƯỢC LƯU TỰ ĐỘNG",
      "settings.done": "HOÀN TẤT", "settings.mobile": "TỐI ƯU CHO THIẾT BỊ MOBILE",
      "settings.audio.title": "ÂM THANH MENU", "settings.audio.subtitle": "Điều chỉnh nhạc nền và hiệu ứng giao diện",
      "settings.audio.master": "ÂM LƯỢNG TỔNG", "settings.audio.masterHint": "Điều khiển toàn bộ âm thanh menu",
      "settings.audio.music": "NHẠC NỀN MENU", "settings.audio.musicHint": "Nhạc không khí thành phố khi ở menu",
      "settings.audio.sfx": "HIỆU ỨNG GIAO DIỆN", "settings.audio.sfxHint": "Chạm, xác nhận, cảnh báo và chuyển màn",
      "settings.audio.note": "Các thanh này điều khiển âm thanh của menu. Âm thanh gameplay sẽ dùng bộ trộn riêng khi được kết nối.",
      "settings.language.title": "NGÔN NGỮ GIAO DIỆN", "settings.language.subtitle": "Chọn ngôn ngữ phù hợp với khu vực của bạn",
      "settings.language.note": "Ngôn ngữ được lưu trên thiết bị và áp dụng ngay cho giao diện menu.",
      "settings.lock.unsupported": "KHÔNG HỖ TRỢ TRÊN THIẾT BỊ", "settings.lock.effect": "Preset đang quản lý hiệu ứng",
      "settings.lock.preset": "Được quản lý bởi preset", "settings.quality.using": "ĐANG DÙNG:",
      "settings.quality.autoDetail": "TỰ CÂN BẰNG THEO FPS", "settings.quality.lowDetail": "ƯU TIÊN PIN • 30 FPS",
      "settings.quality.balancedDetail": "CÂN BẰNG • 60 FPS", "settings.quality.highDetail": "CHẤT LƯỢNG CAO • 60 FPS • TỐN PIN HƠN",
      "settings.profile.low": "THẤP", "settings.profile.balanced": "VỪA", "settings.profile.high": "CAO", "settings.profile.ultra": "RẤT CAO",
      "guest.back": "QUAY LẠI", "guest.backLabel": "Quay lại menu chính", "guest.title": "CHÀO MỪNG",
      "guest.createMode": "TẠO KHÁCH", "guest.loginMode": "ĐĂNG NHẬP", "guest.nameLabel": "TÊN NGƯỜI CHƠI",
      "guest.namePlaceholder": "Nhập tên của bạn", "guest.nameHint": "2–20 • Chữ, số, _ hoặc -", "guest.savedTitle": "HỒ SƠ KHÁCH",
      "guest.savedOnDevice": "Tiến trình được lưu trên thiết bị này.", "guest.localNotice": "Hồ sơ cục bộ • Xóa dữ liệu ứng dụng có thể làm mất tiến trình.",
      "guest.create": "TẠO HỒ SƠ", "guest.update": "CẬP NHẬT HỒ SƠ", "guest.continue": "TIẾP TỤC",
      "guest.empty": "CHƯA CÓ HỒ SƠ", "guest.emptyHint": "Tạo hồ sơ khách để bắt đầu", "guest.player": "NGƯỜI CHƠI",
      "guest.lastNone": "Chưa bắt đầu chơi", "guest.lastSaved": "Tiến trình đã lưu trên máy", "guest.lastToday": "Chơi lần cuối: Hôm nay",
      "guest.lastDate": "Chơi lần cuối: {date}", "guest.error.length": "Tên cần có từ 2 đến 20 ký tự.",
      "guest.error.unsupported": "Tên chứa ký tự không được hỗ trợ.", "guest.error.required": "Tên cần có ít nhất một chữ cái hoặc chữ số.",
      "guest.error.profileInvalid": "Tên người chơi cần 2–20 ký tự hợp lệ.", "guest.error.avatar": "Ảnh đại diện đã chọn không hợp lệ.",
      "guest.response.invalid": "Dữ liệu hồ sơ khách không hợp lệ.", "guest.response.saved": "Hồ sơ khách đã được lưu trên thiết bị.",
      "guest.response.notFound": "Không tìm thấy hồ sơ khách trên thiết bị.", "guest.response.opened": "Đã mở hồ sơ khách trên thiết bị.",
      "guest.response.unconfirmed": "Không thể xác nhận hồ sơ khách.", "guest.response.unityUnavailable": "Không kết nối được Unity để lưu hồ sơ.",
      "guest.response.timeout": "Unity chưa phản hồi. Vui lòng thử lại.", "guest.response.createFirst": "Chưa có hồ sơ khách trên thiết bị. Hãy tạo hồ sơ trước.",
      "guest.response.saving": "Đang lưu hồ sơ khách...", "guest.response.opening": "Đang mở hồ sơ khách trên thiết bị...",
      "runtime.syncOffline": "Đồng bộ đang offline; dữ liệu hiện lưu trên thiết bị.", "runtime.continue.noSave": "Chưa có dữ liệu lưu để tiếp tục.",
      "runtime.saveBusy": "Hệ thống lưu đang bận, vui lòng thử lại.", "runtime.busy.loadingProgress": "ĐANG TẢI TIẾN TRÌNH...",
      "runtime.continue.failed": "Không thể tải dữ liệu. Vui lòng kiểm tra save.", "runtime.mission.invalid": "Màn chơi không hợp lệ.",
      "runtime.mission.completePrevious": "Hoàn thành màn trước để mở khóa màn này.", "runtime.mission.locked": "Màn chơi đã chọn chưa được mở khóa.",
      "runtime.gameplay.sceneMissing": "Scene GamePlay chưa có trong Build Settings.", "runtime.busy.startingCity": "ĐANG KHỞI TẠO THÀNH PHỐ...",
      "runtime.gameplay.failed": "Không thể mở GamePlay. Vui lòng kiểm tra scene.", "runtime.settings.invalid": "Giá trị cài đặt không hợp lệ.",
      "runtime.audio.invalid": "Giá trị âm thanh không hợp lệ.", "runtime.language.invalid": "Ngôn ngữ đã chọn không hợp lệ.",
      "runtime.busy.default": "ĐANG XỬ LÝ...", "runtime.unityDisconnected": "Không kết nối được Unity.",
      "a11y.missionProgress": "Tiến độ nhiệm vụ", "a11y.missionGrid": "Chọn màn chơi", "a11y.backMain": "Quay lại menu chính",
      "a11y.settingsTabs": "Danh mục cài đặt", "a11y.settingsClose": "Đóng cài đặt", "a11y.qualityGroup": "Chất lượng đồ họa",
      "a11y.aaGroup": "Khử răng cưa", "a11y.dofGroup": "Lấy nét DOF", "a11y.avatarGroup": "Chọn ảnh đại diện",
      "a11y.mainMenu": "Menu chính Frank Sandbox Game", "a11y.accountActions": "Tài khoản và đồng bộ", "a11y.primaryActions": "Chức năng chính",
      "a11y.guestModes": "Chọn chế độ hồ sơ khách", "a11y.guestCreatePane": "Tạo hoặc cập nhật hồ sơ khách", "a11y.guestSavedPane": "Hồ sơ khách đã lưu",
      "a11y.avatar1": "Ảnh đại diện 1", "a11y.avatar2": "Ảnh đại diện 2", "a11y.avatar3": "Ảnh đại diện 3", "a11y.languageGroup": "Ngôn ngữ giao diện",
      "settings.graphics.fbsBadge": "NHẸ • ƯU TIÊN MOBILE", "settings.graphics.ssaoBadge": "TỐI ƯU MOBILE • HALF RES",
      "portrait.label": "Yêu cầu xoay màn hình", "portrait.rotate": "XOAY NGANG THIẾT BỊ", "portrait.message": "Frank Sandbox Game được thiết kế cho chế độ landscape.",
      "sync.online": "ĐÃ KẾT NỐI", "sync.offline": "OFFLINE",
      "mission.title": "CHỌN MÀN CHƠI", "mission.progress": "TIẾN ĐỘ", "mission.back": "QUAY LẠI",
      "mission.play": "CHƠI", "mission.help": "Hoàn thành màn trước để mở khóa màn tiếp theo",
      "mission.locked": "KHÓA", "mission.completed": "ĐÃ HOÀN THÀNH", "mission.selected": "ĐANG CHỌN",
      "mission.unlocked": "ĐÃ MỞ KHÓA", "mission.ready": "SẴN SÀNG", "mission.from": "NHIỆM VỤ TỪ",
      "mission.level": "MÀN", "mission.progressText": "Đã hoàn thành {done} trên {total} màn",
      "mission.allComplete": "Bạn đã hoàn thành toàn bộ màn chơi", "mission.unlockTarget": "Hoàn thành màn trước để mở khóa {title}"
    },
    en: {
      "main.profile": "PROFILE", "main.sync": "SYNC", "main.continue": "CONTINUE",
      "main.newGame": "NEW GAME", "main.missions": "MISSIONS", "main.settings": "SETTINGS",
      "main.quit": "QUIT", "main.audio": "AUDIO", "common.on": "ON", "common.off": "OFF",
      "settings.title": "SETTINGS", "settings.description": "Adjust graphics, audio, and language.",
      "settings.tabs.graphics": "GRAPHICS", "settings.tabs.audio": "AUDIO", "settings.tabs.language": "LANGUAGE",
      "settings.graphics.quality": "QUALITY", "settings.graphics.qualityHint": "Automatically optimized for this device",
      "settings.graphics.low": "LOW", "settings.graphics.balanced": "BALANCED", "settings.graphics.high": "HIGH",
      "settings.graphics.aa": "ANTI-ALIASING", "settings.graphics.dof": "DEPTH OF FIELD",
      "settings.graphics.near": "NEAR", "settings.graphics.far": "FAR", "settings.graphics.fbs": "CHARACTER SHADOWS",
      "settings.graphics.fbsHint": "Soft, low-cost shadows", "settings.graphics.ssao": "ENVIRONMENT SHADOWS",
      "settings.graphics.ssaoHint": "Adds depth to the scene", "settings.autosave": "CHANGES SAVE AUTOMATICALLY",
      "settings.done": "DONE", "settings.mobile": "OPTIMIZED FOR MOBILE DEVICES",
      "settings.audio.title": "MENU AUDIO", "settings.audio.subtitle": "Adjust ambient music and interface effects",
      "settings.audio.master": "MASTER VOLUME", "settings.audio.masterHint": "Controls all menu audio",
      "settings.audio.music": "MENU MUSIC", "settings.audio.musicHint": "City ambience while browsing menus",
      "settings.audio.sfx": "INTERFACE EFFECTS", "settings.audio.sfxHint": "Taps, confirmations, warnings, and transitions",
      "settings.audio.note": "These controls affect menu audio. Gameplay audio will use its own mixer once connected.",
      "settings.language.title": "INTERFACE LANGUAGE", "settings.language.subtitle": "Choose the language for your region",
      "settings.language.note": "The language is saved on this device and applied to the menu immediately.",
      "settings.lock.unsupported": "NOT SUPPORTED ON THIS DEVICE", "settings.lock.effect": "This effect is managed by the preset",
      "settings.lock.preset": "Managed by the preset", "settings.quality.using": "USING:",
      "settings.quality.autoDetail": "AUTOMATIC FPS BALANCING", "settings.quality.lowDetail": "BATTERY FIRST • 30 FPS",
      "settings.quality.balancedDetail": "BALANCED • 60 FPS", "settings.quality.highDetail": "HIGH QUALITY • 60 FPS • MORE POWER",
      "settings.profile.low": "LOW", "settings.profile.balanced": "BALANCED", "settings.profile.high": "HIGH", "settings.profile.ultra": "ULTRA",
      "guest.back": "BACK", "guest.backLabel": "Back to main menu", "guest.title": "WELCOME",
      "guest.createMode": "CREATE GUEST", "guest.loginMode": "SIGN IN", "guest.nameLabel": "PLAYER NAME",
      "guest.namePlaceholder": "Enter your name", "guest.nameHint": "2–20 • Letters, numbers, _ or -", "guest.savedTitle": "GUEST PROFILE",
      "guest.savedOnDevice": "Progress is saved on this device.", "guest.localNotice": "Local profile • Clearing app data may erase progress.",
      "guest.create": "CREATE PROFILE", "guest.update": "UPDATE PROFILE", "guest.continue": "CONTINUE",
      "guest.empty": "NO PROFILE", "guest.emptyHint": "Create a guest profile to begin", "guest.player": "PLAYER",
      "guest.lastNone": "Not played yet", "guest.lastSaved": "Progress saved on device", "guest.lastToday": "Last played: Today",
      "guest.lastDate": "Last played: {date}", "guest.error.length": "Name must be 2 to 20 characters.",
      "guest.error.unsupported": "The name contains an unsupported character.", "guest.error.required": "Use at least one letter or number.",
      "guest.error.profileInvalid": "Use a valid player name with 2–20 characters.", "guest.error.avatar": "The selected avatar is not valid.",
      "guest.response.invalid": "The guest profile data is not valid.", "guest.response.saved": "Guest profile saved on this device.",
      "guest.response.notFound": "No guest profile was found on this device.", "guest.response.opened": "Guest profile opened on this device.",
      "guest.response.unconfirmed": "The guest profile could not be confirmed.", "guest.response.unityUnavailable": "Unity is unavailable, so the profile could not be saved.",
      "guest.response.timeout": "Unity did not respond. Please try again.", "guest.response.createFirst": "No guest profile exists on this device. Create one first.",
      "guest.response.saving": "Saving guest profile...", "guest.response.opening": "Opening guest profile on this device...",
      "runtime.syncOffline": "Sync is offline; data is stored on this device.", "runtime.continue.noSave": "There is no saved game to continue.",
      "runtime.saveBusy": "The save system is busy. Please try again.", "runtime.busy.loadingProgress": "LOADING PROGRESS...",
      "runtime.continue.failed": "The saved game could not be loaded.", "runtime.mission.invalid": "This mission is not valid.",
      "runtime.mission.completePrevious": "Complete the previous mission to unlock this one.", "runtime.mission.locked": "The selected mission is still locked.",
      "runtime.gameplay.sceneMissing": "The GamePlay scene is missing from Build Settings.", "runtime.busy.startingCity": "STARTING THE CITY...",
      "runtime.gameplay.failed": "GamePlay could not be opened. Check the scene setup.", "runtime.settings.invalid": "The settings value is not valid.",
      "runtime.audio.invalid": "The audio value is not valid.", "runtime.language.invalid": "The selected language is not valid.",
      "runtime.busy.default": "PROCESSING...", "runtime.unityDisconnected": "Could not connect to Unity.",
      "a11y.missionProgress": "Mission progress", "a11y.missionGrid": "Select a mission", "a11y.backMain": "Back to main menu",
      "a11y.settingsTabs": "Settings categories", "a11y.settingsClose": "Close settings", "a11y.qualityGroup": "Graphics quality",
      "a11y.aaGroup": "Anti-aliasing", "a11y.dofGroup": "Depth of field", "a11y.avatarGroup": "Choose an avatar",
      "a11y.mainMenu": "Frank Sandbox Game main menu", "a11y.accountActions": "Profile and sync", "a11y.primaryActions": "Main actions",
      "a11y.guestModes": "Choose guest profile mode", "a11y.guestCreatePane": "Create or update guest profile", "a11y.guestSavedPane": "Saved guest profile",
      "a11y.avatar1": "Avatar 1", "a11y.avatar2": "Avatar 2", "a11y.avatar3": "Avatar 3", "a11y.languageGroup": "Interface language",
      "settings.graphics.fbsBadge": "LIGHT • MOBILE FIRST", "settings.graphics.ssaoBadge": "MOBILE OPTIMIZED • HALF RES",
      "portrait.label": "Screen rotation required", "portrait.rotate": "ROTATE YOUR DEVICE", "portrait.message": "Frank Sandbox Game is designed for landscape mode.",
      "sync.online": "CONNECTED", "sync.offline": "OFFLINE",
      "mission.title": "SELECT MISSION", "mission.progress": "PROGRESS", "mission.back": "BACK",
      "mission.play": "PLAY", "mission.help": "Complete the previous mission to unlock the next one",
      "mission.locked": "LOCKED", "mission.completed": "COMPLETED", "mission.selected": "SELECTED",
      "mission.unlocked": "UNLOCKED", "mission.ready": "READY", "mission.from": "MISSION FROM",
      "mission.level": "MISSION", "mission.progressText": "Completed {done} of {total} missions",
      "mission.allComplete": "All missions completed", "mission.unlockTarget": "Complete the previous mission to unlock {title}"
    },
    "zh-Hans": {
      "main.profile": "档案", "main.sync": "同步", "main.continue": "继续游戏",
      "main.newGame": "新游戏", "main.missions": "任务", "main.settings": "设置",
      "main.quit": "退出", "main.audio": "声音", "common.on": "开", "common.off": "关",
      "settings.title": "设置", "settings.description": "调整画面、声音和语言。",
      "settings.tabs.graphics": "画面", "settings.tabs.audio": "声音", "settings.tabs.language": "语言",
      "settings.graphics.quality": "画质", "settings.graphics.qualityHint": "根据设备自动优化",
      "settings.graphics.low": "低", "settings.graphics.balanced": "均衡", "settings.graphics.high": "高",
      "settings.graphics.aa": "抗锯齿", "settings.graphics.dof": "景深",
      "settings.graphics.near": "近", "settings.graphics.far": "远", "settings.graphics.fbs": "角色阴影",
      "settings.graphics.fbsHint": "柔和且低负载", "settings.graphics.ssao": "环境阴影",
      "settings.graphics.ssaoHint": "增强场景层次", "settings.autosave": "更改将自动保存",
      "settings.done": "完成", "settings.mobile": "为移动设备优化",
      "settings.audio.title": "菜单声音", "settings.audio.subtitle": "调节背景音乐和界面音效",
      "settings.audio.master": "总音量", "settings.audio.masterHint": "控制全部菜单声音",
      "settings.audio.music": "菜单音乐", "settings.audio.musicHint": "浏览菜单时的城市氛围音乐",
      "settings.audio.sfx": "界面音效", "settings.audio.sfxHint": "点击、确认、警告和转场",
      "settings.audio.note": "这些选项仅控制菜单声音。连接后，游戏声音将使用独立混音器。",
      "settings.language.title": "界面语言", "settings.language.subtitle": "选择适合所在地区的语言",
      "settings.language.note": "语言保存在本设备，并立即应用于菜单界面。",
      "settings.lock.unsupported": "此设备不支持", "settings.lock.effect": "该效果由预设管理",
      "settings.lock.preset": "由预设管理", "settings.quality.using": "当前使用：",
      "settings.quality.autoDetail": "自动平衡帧率", "settings.quality.lowDetail": "省电优先 • 30 FPS",
      "settings.quality.balancedDetail": "均衡 • 60 FPS", "settings.quality.highDetail": "高画质 • 60 FPS • 更耗电",
      "settings.profile.low": "低", "settings.profile.balanced": "均衡", "settings.profile.high": "高", "settings.profile.ultra": "极高",
      "guest.back": "返回", "guest.backLabel": "返回主菜单", "guest.title": "欢迎",
      "guest.createMode": "创建访客", "guest.loginMode": "登录", "guest.nameLabel": "玩家名称",
      "guest.namePlaceholder": "输入你的名字", "guest.nameHint": "2–20 • 字母、数字、_ 或 -", "guest.savedTitle": "访客档案",
      "guest.savedOnDevice": "进度保存在本设备。", "guest.localNotice": "本地档案 • 清除应用数据可能会丢失进度。",
      "guest.create": "创建档案", "guest.update": "更新档案", "guest.continue": "继续",
      "guest.empty": "暂无档案", "guest.emptyHint": "创建访客档案以开始", "guest.player": "玩家",
      "guest.lastNone": "尚未开始", "guest.lastSaved": "进度已保存到设备", "guest.lastToday": "上次游玩：今天",
      "guest.lastDate": "上次游玩：{date}", "guest.error.length": "名称需要 2 到 20 个字符。",
      "guest.error.unsupported": "名称包含不支持的字符。", "guest.error.required": "名称至少需要一个字母或数字。",
      "guest.error.profileInvalid": "请输入 2–20 个有效字符的玩家名称。", "guest.error.avatar": "所选头像无效。",
      "guest.response.invalid": "访客档案数据无效。", "guest.response.saved": "访客档案已保存到本设备。",
      "guest.response.notFound": "本设备上没有找到访客档案。", "guest.response.opened": "已打开本设备上的访客档案。",
      "guest.response.unconfirmed": "无法确认访客档案。", "guest.response.unityUnavailable": "无法连接 Unity，档案未保存。",
      "guest.response.timeout": "Unity 未响应，请重试。", "guest.response.createFirst": "本设备没有访客档案，请先创建。",
      "guest.response.saving": "正在保存访客档案...", "guest.response.opening": "正在打开本地访客档案...",
      "runtime.syncOffline": "同步处于离线状态；数据保存在本设备。", "runtime.continue.noSave": "没有可继续的存档。",
      "runtime.saveBusy": "存档系统正忙，请稍后重试。", "runtime.busy.loadingProgress": "正在加载进度...",
      "runtime.continue.failed": "无法加载存档。", "runtime.mission.invalid": "任务无效。",
      "runtime.mission.completePrevious": "完成前一个任务以解锁此任务。", "runtime.mission.locked": "所选任务尚未解锁。",
      "runtime.gameplay.sceneMissing": "Build Settings 中缺少 GamePlay 场景。", "runtime.busy.startingCity": "正在启动城市...",
      "runtime.gameplay.failed": "无法打开 GamePlay，请检查场景设置。", "runtime.settings.invalid": "设置值无效。",
      "runtime.audio.invalid": "声音设置值无效。", "runtime.language.invalid": "所选语言无效。",
      "runtime.busy.default": "处理中...", "runtime.unityDisconnected": "无法连接 Unity。",
      "a11y.missionProgress": "任务进度", "a11y.missionGrid": "选择任务", "a11y.backMain": "返回主菜单",
      "a11y.settingsTabs": "设置分类", "a11y.settingsClose": "关闭设置", "a11y.qualityGroup": "画质",
      "a11y.aaGroup": "抗锯齿", "a11y.dofGroup": "景深", "a11y.avatarGroup": "选择头像",
      "a11y.mainMenu": "Frank Sandbox Game 主菜单", "a11y.accountActions": "档案与同步", "a11y.primaryActions": "主要操作",
      "a11y.guestModes": "选择访客档案模式", "a11y.guestCreatePane": "创建或更新访客档案", "a11y.guestSavedPane": "已保存的访客档案",
      "a11y.avatar1": "头像 1", "a11y.avatar2": "头像 2", "a11y.avatar3": "头像 3", "a11y.languageGroup": "界面语言",
      "settings.graphics.fbsBadge": "轻量 • 移动优先", "settings.graphics.ssaoBadge": "移动优化 • 半分辨率",
      "portrait.label": "需要旋转屏幕", "portrait.rotate": "请横向旋转设备", "portrait.message": "Frank Sandbox Game 专为横屏模式设计。",
      "sync.online": "已连接", "sync.offline": "离线",
      "mission.title": "选择任务", "mission.progress": "进度", "mission.back": "返回",
      "mission.play": "开始", "mission.help": "完成前一个任务以解锁下一个任务",
      "mission.locked": "未解锁", "mission.completed": "已完成", "mission.selected": "已选择",
      "mission.unlocked": "已解锁", "mission.ready": "就绪", "mission.from": "任务发布者",
      "mission.level": "任务", "mission.progressText": "已完成 {done}/{total} 个任务",
      "mission.allComplete": "所有任务均已完成", "mission.unlockTarget": "完成前一个任务以解锁 {title}"
    },
    ru: {
      "main.profile": "ПРОФИЛЬ", "main.sync": "СИНХРОН.", "main.continue": "ПРОДОЛЖИТЬ",
      "main.newGame": "НОВАЯ ИГРА", "main.missions": "ЗАДАНИЯ", "main.settings": "НАСТРОЙКИ",
      "main.quit": "ВЫХОД", "main.audio": "ЗВУК", "common.on": "ВКЛ", "common.off": "ВЫКЛ",
      "settings.title": "НАСТРОЙКИ", "settings.description": "Настройка графики, звука и языка.",
      "settings.tabs.graphics": "ГРАФИКА", "settings.tabs.audio": "ЗВУК", "settings.tabs.language": "ЯЗЫК",
      "settings.graphics.quality": "КАЧЕСТВО", "settings.graphics.qualityHint": "Автооптимизация для устройства",
      "settings.graphics.low": "НИЗКО", "settings.graphics.balanced": "БАЛАНС", "settings.graphics.high": "ВЫСОКО",
      "settings.graphics.aa": "СГЛАЖИВАНИЕ", "settings.graphics.dof": "ГЛУБИНА РЕЗКОСТИ",
      "settings.graphics.near": "БЛИЗКО", "settings.graphics.far": "ДАЛЕКО", "settings.graphics.fbs": "ТЕНИ ПЕРСОНАЖА",
      "settings.graphics.fbsHint": "Мягкие и лёгкие тени", "settings.graphics.ssao": "ТЕНИ ОКРУЖЕНИЯ",
      "settings.graphics.ssaoHint": "Добавляет глубину сцене", "settings.autosave": "ИЗМЕНЕНИЯ СОХРАНЯЮТСЯ АВТОМАТИЧЕСКИ",
      "settings.done": "ГОТОВО", "settings.mobile": "ОПТИМИЗИРОВАНО ДЛЯ МОБИЛЬНЫХ УСТРОЙСТВ",
      "settings.audio.title": "ЗВУК МЕНЮ", "settings.audio.subtitle": "Музыка и звуки интерфейса",
      "settings.audio.master": "ОБЩАЯ ГРОМКОСТЬ", "settings.audio.masterHint": "Управляет всем звуком меню",
      "settings.audio.music": "МУЗЫКА МЕНЮ", "settings.audio.musicHint": "Городская атмосфера в меню",
      "settings.audio.sfx": "ЗВУКИ ИНТЕРФЕЙСА", "settings.audio.sfxHint": "Нажатия, подтверждения и переходы",
      "settings.audio.note": "Эти параметры управляют звуком меню. Звук игры будет использовать отдельный микшер.",
      "settings.language.title": "ЯЗЫК ИНТЕРФЕЙСА", "settings.language.subtitle": "Выберите язык вашего региона",
      "settings.language.note": "Язык хранится на устройстве и применяется к меню сразу.",
      "settings.lock.unsupported": "НЕ ПОДДЕРЖИВАЕТСЯ", "settings.lock.effect": "Эффект управляется пресетом",
      "settings.lock.preset": "Управляется пресетом", "settings.quality.using": "ИСПОЛЬЗУЕТСЯ:",
      "settings.quality.autoDetail": "АВТОБАЛАНС FPS", "settings.quality.lowDetail": "ЭКОНОМИЯ БАТАРЕИ • 30 FPS",
      "settings.quality.balancedDetail": "БАЛАНС • 60 FPS", "settings.quality.highDetail": "ВЫСОКОЕ КАЧЕСТВО • 60 FPS • БОЛЬШЕ ЭНЕРГИИ",
      "settings.profile.low": "НИЗКО", "settings.profile.balanced": "БАЛАНС", "settings.profile.high": "ВЫСОКО", "settings.profile.ultra": "УЛЬТРА",
      "guest.back": "НАЗАД", "guest.backLabel": "Назад в главное меню", "guest.title": "ДОБРО ПОЖАЛОВАТЬ",
      "guest.createMode": "СОЗДАТЬ ГОСТЯ", "guest.loginMode": "ВОЙТИ", "guest.nameLabel": "ИМЯ ИГРОКА",
      "guest.namePlaceholder": "Введите имя", "guest.nameHint": "2–20 • Буквы, цифры, _ или -", "guest.savedTitle": "ГОСТЕВОЙ ПРОФИЛЬ",
      "guest.savedOnDevice": "Прогресс хранится на устройстве.", "guest.localNotice": "Локальный профиль • Очистка данных может удалить прогресс.",
      "guest.create": "СОЗДАТЬ ПРОФИЛЬ", "guest.update": "ОБНОВИТЬ ПРОФИЛЬ", "guest.continue": "ПРОДОЛЖИТЬ",
      "guest.empty": "НЕТ ПРОФИЛЯ", "guest.emptyHint": "Создайте гостевой профиль", "guest.player": "ИГРОК",
      "guest.lastNone": "Игра ещё не начата", "guest.lastSaved": "Прогресс сохранён", "guest.lastToday": "Последняя игра: сегодня",
      "guest.lastDate": "Последняя игра: {date}", "guest.error.length": "Имя должно содержать от 2 до 20 символов.",
      "guest.error.unsupported": "Имя содержит неподдерживаемый символ.", "guest.error.required": "Добавьте хотя бы одну букву или цифру.",
      "guest.error.profileInvalid": "Имя игрока должно содержать 2–20 допустимых символов.", "guest.error.avatar": "Выбран недопустимый аватар.",
      "guest.response.invalid": "Данные гостевого профиля недействительны.", "guest.response.saved": "Гостевой профиль сохранён на устройстве.",
      "guest.response.notFound": "Гостевой профиль на устройстве не найден.", "guest.response.opened": "Гостевой профиль открыт на устройстве.",
      "guest.response.unconfirmed": "Не удалось подтвердить гостевой профиль.", "guest.response.unityUnavailable": "Нет соединения с Unity; профиль не сохранён.",
      "guest.response.timeout": "Unity не ответил. Повторите попытку.", "guest.response.createFirst": "На устройстве нет гостевого профиля. Сначала создайте его.",
      "guest.response.saving": "Сохранение гостевого профиля...", "guest.response.opening": "Открытие гостевого профиля...",
      "runtime.syncOffline": "Синхронизация офлайн; данные хранятся на устройстве.", "runtime.continue.noSave": "Нет сохранения для продолжения.",
      "runtime.saveBusy": "Система сохранений занята. Повторите попытку.", "runtime.busy.loadingProgress": "ЗАГРУЗКА ПРОГРЕССА...",
      "runtime.continue.failed": "Не удалось загрузить сохранение.", "runtime.mission.invalid": "Недопустимое задание.",
      "runtime.mission.completePrevious": "Завершите предыдущее задание, чтобы открыть это.", "runtime.mission.locked": "Выбранное задание ещё закрыто.",
      "runtime.gameplay.sceneMissing": "Сцена GamePlay отсутствует в Build Settings.", "runtime.busy.startingCity": "ЗАПУСК ГОРОДА...",
      "runtime.gameplay.failed": "Не удалось открыть GamePlay. Проверьте сцену.", "runtime.settings.invalid": "Недопустимое значение настройки.",
      "runtime.audio.invalid": "Недопустимое значение звука.", "runtime.language.invalid": "Недопустимый язык.",
      "runtime.busy.default": "ОБРАБОТКА...", "runtime.unityDisconnected": "Нет соединения с Unity.",
      "a11y.missionProgress": "Прогресс заданий", "a11y.missionGrid": "Выбор задания", "a11y.backMain": "Назад в главное меню",
      "a11y.settingsTabs": "Категории настроек", "a11y.settingsClose": "Закрыть настройки", "a11y.qualityGroup": "Качество графики",
      "a11y.aaGroup": "Сглаживание", "a11y.dofGroup": "Глубина резкости", "a11y.avatarGroup": "Выбор аватара",
      "a11y.mainMenu": "Главное меню Frank Sandbox Game", "a11y.accountActions": "Профиль и синхронизация", "a11y.primaryActions": "Основные действия",
      "a11y.guestModes": "Режим гостевого профиля", "a11y.guestCreatePane": "Создать или обновить профиль", "a11y.guestSavedPane": "Сохранённый гостевой профиль",
      "a11y.avatar1": "Аватар 1", "a11y.avatar2": "Аватар 2", "a11y.avatar3": "Аватар 3", "a11y.languageGroup": "Язык интерфейса",
      "settings.graphics.fbsBadge": "ЛЁГКИЕ • ДЛЯ МОБИЛЬНЫХ", "settings.graphics.ssaoBadge": "МОБИЛЬНАЯ ОПТИМИЗАЦИЯ • HALF RES",
      "portrait.label": "Поверните экран", "portrait.rotate": "ПОВЕРНИТЕ УСТРОЙСТВО", "portrait.message": "Frank Sandbox Game рассчитана на альбомный режим.",
      "sync.online": "ПОДКЛЮЧЕНО", "sync.offline": "ОФЛАЙН",
      "mission.title": "ВЫБОР ЗАДАНИЯ", "mission.progress": "ПРОГРЕСС", "mission.back": "НАЗАД",
      "mission.play": "ИГРАТЬ", "mission.help": "Завершите предыдущее задание, чтобы открыть следующее",
      "mission.locked": "ЗАКРЫТО", "mission.completed": "ЗАВЕРШЕНО", "mission.selected": "ВЫБРАНО",
      "mission.unlocked": "ОТКРЫТО", "mission.ready": "ГОТОВО", "mission.from": "ЗАДАНИЕ ОТ",
      "mission.level": "ЗАДАНИЕ", "mission.progressText": "Завершено {done} из {total} заданий",
      "mission.allComplete": "Все задания завершены", "mission.unlockTarget": "Завершите предыдущее задание, чтобы открыть {title}"
    },
    "pt-BR": {
      "main.profile": "PERFIL", "main.sync": "SINCRONIZAR", "main.continue": "CONTINUAR",
      "main.newGame": "NOVO JOGO", "main.missions": "MISSÕES", "main.settings": "CONFIGURAÇÕES",
      "main.quit": "SAIR", "main.audio": "ÁUDIO", "common.on": "LIG.", "common.off": "DESL.",
      "settings.title": "CONFIGURAÇÕES", "settings.description": "Ajuste gráficos, áudio e idioma.",
      "settings.tabs.graphics": "GRÁFICOS", "settings.tabs.audio": "ÁUDIO", "settings.tabs.language": "IDIOMA",
      "settings.graphics.quality": "QUALIDADE", "settings.graphics.qualityHint": "Otimização automática para o aparelho",
      "settings.graphics.low": "BAIXA", "settings.graphics.balanced": "EQUILIBRADA", "settings.graphics.high": "ALTA",
      "settings.graphics.aa": "ANTISSERRILHADO", "settings.graphics.dof": "PROFUNDIDADE DE CAMPO",
      "settings.graphics.near": "PERTO", "settings.graphics.far": "LONGE", "settings.graphics.fbs": "SOMBRAS DO PERSONAGEM",
      "settings.graphics.fbsHint": "Sombras suaves e leves", "settings.graphics.ssao": "SOMBRAS DO AMBIENTE",
      "settings.graphics.ssaoHint": "Aumenta a profundidade da cena", "settings.autosave": "ALTERAÇÕES SALVAS AUTOMATICAMENTE",
      "settings.done": "CONCLUÍDO", "settings.mobile": "OTIMIZADO PARA DISPOSITIVOS MÓVEIS",
      "settings.audio.title": "ÁUDIO DO MENU", "settings.audio.subtitle": "Ajuste a música e os efeitos da interface",
      "settings.audio.master": "VOLUME GERAL", "settings.audio.masterHint": "Controla todo o áudio do menu",
      "settings.audio.music": "MÚSICA DO MENU", "settings.audio.musicHint": "Ambiente urbano durante os menus",
      "settings.audio.sfx": "EFEITOS DA INTERFACE", "settings.audio.sfxHint": "Toques, confirmações, avisos e transições",
      "settings.audio.note": "Estes controles afetam o menu. O áudio do jogo usará um mixer separado quando conectado.",
      "settings.language.title": "IDIOMA DA INTERFACE", "settings.language.subtitle": "Escolha o idioma da sua região",
      "settings.language.note": "O idioma fica salvo neste dispositivo e é aplicado ao menu imediatamente.",
      "settings.lock.unsupported": "NÃO DISPONÍVEL NESTE DISPOSITIVO", "settings.lock.effect": "Efeito controlado pelo preset",
      "settings.lock.preset": "Controlado pelo preset", "settings.quality.using": "EM USO:",
      "settings.quality.autoDetail": "BALANCEAMENTO AUTOMÁTICO DE FPS", "settings.quality.lowDetail": "ECONOMIA DE BATERIA • 30 FPS",
      "settings.quality.balancedDetail": "EQUILIBRADO • 60 FPS", "settings.quality.highDetail": "ALTA QUALIDADE • 60 FPS • MAIOR CONSUMO",
      "settings.profile.low": "BAIXA", "settings.profile.balanced": "EQUILIBRADA", "settings.profile.high": "ALTA", "settings.profile.ultra": "ULTRA",
      "guest.back": "VOLTAR", "guest.backLabel": "Voltar ao menu principal", "guest.title": "BOAS-VINDAS",
      "guest.createMode": "CRIAR CONVIDADO", "guest.loginMode": "ENTRAR", "guest.nameLabel": "NOME DO JOGADOR",
      "guest.namePlaceholder": "Digite seu nome", "guest.nameHint": "2–20 • Letras, números, _ ou -", "guest.savedTitle": "PERFIL DE CONVIDADO",
      "guest.savedOnDevice": "O progresso fica salvo neste dispositivo.", "guest.localNotice": "Perfil local • Limpar os dados pode apagar o progresso.",
      "guest.create": "CRIAR PERFIL", "guest.update": "ATUALIZAR PERFIL", "guest.continue": "CONTINUAR",
      "guest.empty": "SEM PERFIL", "guest.emptyHint": "Crie um perfil de convidado", "guest.player": "JOGADOR",
      "guest.lastNone": "Ainda não jogou", "guest.lastSaved": "Progresso salvo no dispositivo", "guest.lastToday": "Última sessão: hoje",
      "guest.lastDate": "Última sessão: {date}", "guest.error.length": "O nome deve ter de 2 a 20 caracteres.",
      "guest.error.unsupported": "O nome contém um caractere não compatível.", "guest.error.required": "Use pelo menos uma letra ou número.",
      "guest.error.profileInvalid": "Use um nome de jogador válido com 2–20 caracteres.", "guest.error.avatar": "O avatar escolhido não é válido.",
      "guest.response.invalid": "Os dados do perfil de convidado não são válidos.", "guest.response.saved": "Perfil de convidado salvo neste dispositivo.",
      "guest.response.notFound": "Nenhum perfil de convidado foi encontrado neste dispositivo.", "guest.response.opened": "Perfil de convidado aberto neste dispositivo.",
      "guest.response.unconfirmed": "Não foi possível confirmar o perfil de convidado.", "guest.response.unityUnavailable": "Não foi possível conectar ao Unity para salvar o perfil.",
      "guest.response.timeout": "O Unity não respondeu. Tente novamente.", "guest.response.createFirst": "Não há perfil de convidado neste dispositivo. Crie um primeiro.",
      "guest.response.saving": "Salvando perfil de convidado...", "guest.response.opening": "Abrindo perfil de convidado...",
      "runtime.syncOffline": "A sincronização está offline; os dados ficam neste dispositivo.", "runtime.continue.noSave": "Não há jogo salvo para continuar.",
      "runtime.saveBusy": "O sistema de salvamento está ocupado. Tente novamente.", "runtime.busy.loadingProgress": "CARREGANDO PROGRESSO...",
      "runtime.continue.failed": "Não foi possível carregar o jogo salvo.", "runtime.mission.invalid": "Esta missão não é válida.",
      "runtime.mission.completePrevious": "Conclua a missão anterior para liberar esta.", "runtime.mission.locked": "A missão escolhida ainda está bloqueada.",
      "runtime.gameplay.sceneMissing": "A cena GamePlay não está no Build Settings.", "runtime.busy.startingCity": "INICIANDO A CIDADE...",
      "runtime.gameplay.failed": "Não foi possível abrir o GamePlay. Verifique a cena.", "runtime.settings.invalid": "O valor de configuração não é válido.",
      "runtime.audio.invalid": "O valor de áudio não é válido.", "runtime.language.invalid": "O idioma escolhido não é válido.",
      "runtime.busy.default": "PROCESSANDO...", "runtime.unityDisconnected": "Não foi possível conectar ao Unity.",
      "a11y.missionProgress": "Progresso das missões", "a11y.missionGrid": "Selecionar missão", "a11y.backMain": "Voltar ao menu principal",
      "a11y.settingsTabs": "Categorias de configuração", "a11y.settingsClose": "Fechar configurações", "a11y.qualityGroup": "Qualidade gráfica",
      "a11y.aaGroup": "Antisserrilhado", "a11y.dofGroup": "Profundidade de campo", "a11y.avatarGroup": "Escolher avatar",
      "a11y.mainMenu": "Menu principal do Frank Sandbox Game", "a11y.accountActions": "Perfil e sincronização", "a11y.primaryActions": "Ações principais",
      "a11y.guestModes": "Modo do perfil de convidado", "a11y.guestCreatePane": "Criar ou atualizar perfil", "a11y.guestSavedPane": "Perfil de convidado salvo",
      "a11y.avatar1": "Avatar 1", "a11y.avatar2": "Avatar 2", "a11y.avatar3": "Avatar 3", "a11y.languageGroup": "Idioma da interface",
      "settings.graphics.fbsBadge": "LEVE • PRIORIDADE MOBILE", "settings.graphics.ssaoBadge": "OTIMIZADO PARA MOBILE • HALF RES",
      "portrait.label": "É necessário girar a tela", "portrait.rotate": "GIRE O DISPOSITIVO", "portrait.message": "Frank Sandbox Game foi projetado para o modo paisagem.",
      "sync.online": "CONECTADO", "sync.offline": "OFFLINE",
      "mission.title": "SELECIONAR MISSÃO", "mission.progress": "PROGRESSO", "mission.back": "VOLTAR",
      "mission.play": "JOGAR", "mission.help": "Conclua a missão anterior para liberar a próxima",
      "mission.locked": "BLOQUEADA", "mission.completed": "CONCLUÍDA", "mission.selected": "SELECIONADA",
      "mission.unlocked": "LIBERADA", "mission.ready": "PRONTA", "mission.from": "MISSÃO DE",
      "mission.level": "MISSÃO", "mission.progressText": "{done} de {total} missões concluídas",
      "mission.allComplete": "Todas as missões foram concluídas", "mission.unlockTarget": "Conclua a missão anterior para liberar {title}"
    },
    "es-419": {
      "main.profile": "PERFIL", "main.sync": "SINCRONIZAR", "main.continue": "CONTINUAR",
      "main.newGame": "NUEVA PARTIDA", "main.missions": "MISIONES", "main.settings": "AJUSTES",
      "main.quit": "SALIR", "main.audio": "AUDIO", "common.on": "SÍ", "common.off": "NO",
      "settings.title": "AJUSTES", "settings.description": "Ajusta gráficos, audio e idioma.",
      "settings.tabs.graphics": "GRÁFICOS", "settings.tabs.audio": "AUDIO", "settings.tabs.language": "IDIOMA",
      "settings.graphics.quality": "CALIDAD", "settings.graphics.qualityHint": "Optimización automática para el dispositivo",
      "settings.graphics.low": "BAJA", "settings.graphics.balanced": "EQUILIBRADA", "settings.graphics.high": "ALTA",
      "settings.graphics.aa": "ANTIALIASING", "settings.graphics.dof": "PROFUNDIDAD DE CAMPO",
      "settings.graphics.near": "CERCA", "settings.graphics.far": "LEJOS", "settings.graphics.fbs": "SOMBRAS DEL PERSONAJE",
      "settings.graphics.fbsHint": "Sombras suaves y ligeras", "settings.graphics.ssao": "SOMBRAS DEL ENTORNO",
      "settings.graphics.ssaoHint": "Añade profundidad a la escena", "settings.autosave": "LOS CAMBIOS SE GUARDAN AUTOMÁTICAMENTE",
      "settings.done": "LISTO", "settings.mobile": "OPTIMIZADO PARA DISPOSITIVOS MÓVILES",
      "settings.audio.title": "AUDIO DEL MENÚ", "settings.audio.subtitle": "Ajusta la música y los efectos de interfaz",
      "settings.audio.master": "VOLUMEN GENERAL", "settings.audio.masterHint": "Controla todo el audio del menú",
      "settings.audio.music": "MÚSICA DEL MENÚ", "settings.audio.musicHint": "Ambiente urbano mientras navegas",
      "settings.audio.sfx": "EFECTOS DE INTERFAZ", "settings.audio.sfxHint": "Toques, confirmaciones, avisos y transiciones",
      "settings.audio.note": "Estos controles afectan al menú. El audio del juego usará un mezclador separado al conectarse.",
      "settings.language.title": "IDIOMA DE INTERFAZ", "settings.language.subtitle": "Elige el idioma de tu región",
      "settings.language.note": "El idioma se guarda en este dispositivo y se aplica al menú de inmediato.",
      "settings.lock.unsupported": "NO DISPONIBLE EN ESTE DISPOSITIVO", "settings.lock.effect": "El preset controla este efecto",
      "settings.lock.preset": "Controlado por el preset", "settings.quality.using": "EN USO:",
      "settings.quality.autoDetail": "BALANCE AUTOMÁTICO DE FPS", "settings.quality.lowDetail": "AHORRO DE BATERÍA • 30 FPS",
      "settings.quality.balancedDetail": "EQUILIBRADO • 60 FPS", "settings.quality.highDetail": "ALTA CALIDAD • 60 FPS • MAYOR CONSUMO",
      "settings.profile.low": "BAJA", "settings.profile.balanced": "EQUILIBRADA", "settings.profile.high": "ALTA", "settings.profile.ultra": "ULTRA",
      "guest.back": "VOLVER", "guest.backLabel": "Volver al menú principal", "guest.title": "BIENVENIDO",
      "guest.createMode": "CREAR INVITADO", "guest.loginMode": "INICIAR SESIÓN", "guest.nameLabel": "NOMBRE DEL JUGADOR",
      "guest.namePlaceholder": "Escribe tu nombre", "guest.nameHint": "2–20 • Letras, números, _ o -", "guest.savedTitle": "PERFIL DE INVITADO",
      "guest.savedOnDevice": "El progreso se guarda en este dispositivo.", "guest.localNotice": "Perfil local • Borrar los datos puede eliminar el progreso.",
      "guest.create": "CREAR PERFIL", "guest.update": "ACTUALIZAR PERFIL", "guest.continue": "CONTINUAR",
      "guest.empty": "SIN PERFIL", "guest.emptyHint": "Crea un perfil de invitado", "guest.player": "JUGADOR",
      "guest.lastNone": "Aún no has jugado", "guest.lastSaved": "Progreso guardado", "guest.lastToday": "Última partida: hoy",
      "guest.lastDate": "Última partida: {date}", "guest.error.length": "El nombre debe tener entre 2 y 20 caracteres.",
      "guest.error.unsupported": "El nombre contiene un carácter no compatible.", "guest.error.required": "Usa al menos una letra o un número.",
      "guest.error.profileInvalid": "Usa un nombre de jugador válido de 2–20 caracteres.", "guest.error.avatar": "El avatar seleccionado no es válido.",
      "guest.response.invalid": "Los datos del perfil de invitado no son válidos.", "guest.response.saved": "Perfil de invitado guardado en este dispositivo.",
      "guest.response.notFound": "No se encontró un perfil de invitado en este dispositivo.", "guest.response.opened": "Perfil de invitado abierto en este dispositivo.",
      "guest.response.unconfirmed": "No se pudo confirmar el perfil de invitado.", "guest.response.unityUnavailable": "No se pudo conectar con Unity para guardar el perfil.",
      "guest.response.timeout": "Unity no respondió. Inténtalo de nuevo.", "guest.response.createFirst": "No hay perfil de invitado en este dispositivo. Crea uno primero.",
      "guest.response.saving": "Guardando perfil de invitado...", "guest.response.opening": "Abriendo perfil de invitado...",
      "runtime.syncOffline": "La sincronización está desconectada; los datos se guardan en este dispositivo.", "runtime.continue.noSave": "No hay una partida guardada para continuar.",
      "runtime.saveBusy": "El sistema de guardado está ocupado. Inténtalo de nuevo.", "runtime.busy.loadingProgress": "CARGANDO PROGRESO...",
      "runtime.continue.failed": "No se pudo cargar la partida guardada.", "runtime.mission.invalid": "Esta misión no es válida.",
      "runtime.mission.completePrevious": "Completa la misión anterior para desbloquear esta.", "runtime.mission.locked": "La misión seleccionada sigue bloqueada.",
      "runtime.gameplay.sceneMissing": "La escena GamePlay no está en Build Settings.", "runtime.busy.startingCity": "INICIANDO LA CIUDAD...",
      "runtime.gameplay.failed": "No se pudo abrir GamePlay. Revisa la escena.", "runtime.settings.invalid": "El valor del ajuste no es válido.",
      "runtime.audio.invalid": "El valor de audio no es válido.", "runtime.language.invalid": "El idioma seleccionado no es válido.",
      "runtime.busy.default": "PROCESANDO...", "runtime.unityDisconnected": "No se pudo conectar con Unity.",
      "a11y.missionProgress": "Progreso de misiones", "a11y.missionGrid": "Elegir misión", "a11y.backMain": "Volver al menú principal",
      "a11y.settingsTabs": "Categorías de ajustes", "a11y.settingsClose": "Cerrar ajustes", "a11y.qualityGroup": "Calidad gráfica",
      "a11y.aaGroup": "Antialiasing", "a11y.dofGroup": "Profundidad de campo", "a11y.avatarGroup": "Elegir avatar",
      "a11y.mainMenu": "Menú principal de Frank Sandbox Game", "a11y.accountActions": "Perfil y sincronización", "a11y.primaryActions": "Acciones principales",
      "a11y.guestModes": "Modo del perfil de invitado", "a11y.guestCreatePane": "Crear o actualizar perfil", "a11y.guestSavedPane": "Perfil de invitado guardado",
      "a11y.avatar1": "Avatar 1", "a11y.avatar2": "Avatar 2", "a11y.avatar3": "Avatar 3", "a11y.languageGroup": "Idioma de la interfaz",
      "settings.graphics.fbsBadge": "LIGERO • PRIORIDAD MÓVIL", "settings.graphics.ssaoBadge": "OPTIMIZADO PARA MÓVIL • HALF RES",
      "portrait.label": "Es necesario girar la pantalla", "portrait.rotate": "GIRA EL DISPOSITIVO", "portrait.message": "Frank Sandbox Game está diseñado para el modo horizontal.",
      "sync.online": "CONECTADO", "sync.offline": "SIN CONEXIÓN",
      "mission.title": "ELEGIR MISIÓN", "mission.progress": "PROGRESO", "mission.back": "VOLVER",
      "mission.play": "JUGAR", "mission.help": "Completa la misión anterior para desbloquear la siguiente",
      "mission.locked": "BLOQUEADA", "mission.completed": "COMPLETADA", "mission.selected": "SELECCIONADA",
      "mission.unlocked": "DESBLOQUEADA", "mission.ready": "LISTA", "mission.from": "MISIÓN DE",
      "mission.level": "MISIÓN", "mission.progressText": "{done} de {total} misiones completadas",
      "mission.allComplete": "Completaste todas las misiones", "mission.unlockTarget": "Completa la misión anterior para desbloquear {title}"
    }
  };

  var missionCopy = {
    en: {
      "strawberry-pickup": ["STRAWBERRY PICKUP", "Reach Strawberry and secure a fast vehicle for the pickup. Deliver it to the meeting point without heavy damage."],
      "warehouse-recon": ["WAREHOUSE RECON", "Approach the marked warehouse, stay unseen, and photograph the delivery van."],
      "downtown-vip": ["DOWNTOWN VIP", "Reach the downtown pickup before time expires and deliver the VIP safely."],
      "range-pressure": ["RANGE PRESSURE", "Reach the west shooting range and clear the target quota in three rounds before time expires."],
      "fuel-run": ["HOT DELIVERY", "Collect the sport bike at the fuel station and deliver it to the garage without damage."],
      "carrier-signal": ["OFFSHORE SIGNAL", "Scout the carrier deck with the drone, mark the signal, then leave by gyrocopter without raising an alarm."]
    },
    "zh-Hans": {
      "strawberry-pickup": ["草莓区接头", "前往草莓区取得一辆高速车辆，并尽量保持车辆完好地送到接头点。"],
      "warehouse-recon": ["仓库侦察", "秘密接近标记仓库，并拍下送货厢式车。"],
      "downtown-vip": ["市中心贵宾", "在时间结束前赶到市中心接客，并安全送达贵宾。"],
      "range-pressure": ["靶场压力", "前往西部靶场，在三轮内完成目标数量。"],
      "fuel-run": ["紧急送货", "在加油站取走运动摩托，并在时间结束前完好送到车库。"],
      "carrier-signal": ["海上信号", "用无人机侦察航母甲板，标记信号后乘旋翼机撤离。"]
    },
    ru: {
      "strawberry-pickup": ["ВСТРЕЧА В СТРОБЕРРИ", "Доберитесь до Строберри, найдите быструю машину и доставьте её без серьёзных повреждений."],
      "warehouse-recon": ["РАЗВЕДКА СКЛАДА", "Незаметно подойдите к складу и сфотографируйте фургон доставки."],
      "downtown-vip": ["VIP В ЦЕНТРЕ", "Успейте забрать VIP-клиента в центре и безопасно довезите его."],
      "range-pressure": ["ДАВЛЕНИЕ ТИРА", "Доберитесь до западного тира и поразите цели за три раунда до конца времени."],
      "fuel-run": ["ГОРЯЧАЯ ДОСТАВКА", "Заберите спортбайк у заправки и доставьте его в гараж без повреждений."],
      "carrier-signal": ["СИГНАЛ В МОРЕ", "Разведайте палубу дроном, отметьте сигнал и покиньте район на гирокоптере."]
    },
    "pt-BR": {
      "strawberry-pickup": ["ENCONTRO EM STRAWBERRY", "Chegue a Strawberry, consiga um veículo rápido e entregue-o sem grandes danos."],
      "warehouse-recon": ["RECONHECIMENTO DO DEPÓSITO", "Aproxime-se sem ser visto e fotografe a van de entrega."],
      "downtown-vip": ["VIP NO CENTRO", "Chegue ao ponto de coleta antes do tempo e leve o VIP em segurança."],
      "range-pressure": ["PRESSÃO NO ESTANDE", "Vá ao estande oeste e acerte a cota de alvos em três rodadas."],
      "fuel-run": ["ENTREGA QUENTE", "Pegue a moto no posto e leve-a à garagem sem danos."],
      "carrier-signal": ["SINAL EM ALTO-MAR", "Reconheça o convés com o drone, marque o sinal e saia de girocóptero sem disparar o alarme."]
    },
    "es-419": {
      "strawberry-pickup": ["ENCUENTRO EN STRAWBERRY", "Llega a Strawberry, consigue un vehículo rápido y entrégalo sin daños graves."],
      "warehouse-recon": ["RECONOCIMIENTO DEL ALMACÉN", "Acércate sin ser visto y fotografía la camioneta de reparto."],
      "downtown-vip": ["VIP DEL CENTRO", "Llega al punto de recogida antes de que termine el tiempo y lleva al VIP a salvo."],
      "range-pressure": ["PRESIÓN EN EL CAMPO", "Ve al campo de tiro oeste y alcanza la cuota en tres rondas."],
      "fuel-run": ["ENTREGA CALIENTE", "Recoge la moto en la gasolinera y llévala al garaje sin daños."],
      "carrier-signal": ["SEÑAL EN ALTA MAR", "Explora la cubierta con el dron, marca la señal y escapa en girocóptero sin activar la alarma."]
    }
  };

  var currentLocale = "vi";
  var supportedLocales = ["vi", "en", "zh-Hans", "ru", "pt-BR", "es-419"];

  function register(locale, dictionary, missions) {
    var key = String(locale || "").trim();
    if (!key || !dictionary || typeof dictionary !== "object") return false;
    dictionaries[key] = dictionary;
    missionCopy[key] = missions && typeof missions === "object" ? missions : {};
    if (supportedLocales.indexOf(key) < 0) supportedLocales.push(key);
    return true;
  }

  function extend(locale, entries) {
    var key = normalize(locale);
    var table = dictionaries[key];
    if (!table || !entries || typeof entries !== "object") return false;
    Object.keys(entries).forEach(function (entryKey) {
      table[entryKey] = entries[entryKey];
    });
    return true;
  }

  function normalize(locale) {
    var value = String(locale || "vi").toLowerCase();
    if (value === "zh-hans" || value.indexOf("zh-cn") === 0) return "zh-Hans";
    if (value === "zh-hant" || value.indexOf("zh-tw") === 0 ||
        value.indexOf("zh-hk") === 0 || value.indexOf("zh-mo") === 0) return "zh-Hant";
    if (value === "pt-br" || value.indexOf("pt") === 0) return "pt-BR";
    if (value === "es-419" || value.indexOf("es") === 0) return "es-419";
    if (value.indexOf("de") === 0) return "de";
    if (value.indexOf("fr") === 0) return "fr";
    if (value.indexOf("ja") === 0) return "ja";
    if (value.indexOf("ko") === 0) return "ko";
    if (value.indexOf("nl") === 0) return "nl";
    if (value.indexOf("ru") === 0) return "ru";
    if (value.indexOf("en") === 0) return "en";
    return "vi";
  }

  function t(key, fallback, replacements) {
    var table = dictionaries[currentLocale] || dictionaries.vi;
    var value = table[key] || dictionaries.vi[key] || fallback || key;
    if (replacements) {
      Object.keys(replacements).forEach(function (name) {
        value = value.split("{" + name + "}").join(String(replacements[name]));
      });
    }
    return value;
  }

  function apply(locale) {
    currentLocale = normalize(locale);
    document.documentElement.lang = currentLocale;
    var usesCjkTypography = currentLocale === "zh-Hans" ||
      currentLocale === "zh-Hant" || currentLocale === "ja" || currentLocale === "ko";
    document.documentElement.classList.toggle("locale-cjk", usesCjkTypography);
    document.documentElement.classList.toggle(
      "locale-system",
      usesCjkTypography || currentLocale === "ru"
    );
    Array.prototype.slice.call(document.querySelectorAll("[data-i18n]")).forEach(function (element) {
      element.textContent = t(element.getAttribute("data-i18n"), element.textContent);
    });
    Array.prototype.slice.call(document.querySelectorAll("[data-i18n-placeholder]"))
      .forEach(function (element) {
        element.setAttribute(
          "placeholder",
          t(element.getAttribute("data-i18n-placeholder"), element.getAttribute("placeholder"))
        );
      });
    Array.prototype.slice.call(document.querySelectorAll("[data-i18n-aria-label]"))
      .forEach(function (element) {
        element.setAttribute(
          "aria-label",
          t(element.getAttribute("data-i18n-aria-label"), element.getAttribute("aria-label"))
        );
      });
    return currentLocale;
  }

  function localizeMission(item) {
    if (!item || currentLocale === "vi") return item;
    var localeTable = missionCopy[currentLocale];
    var copy = localeTable && localeTable[item.id];
    if (!copy) return item;
    var localized = {};
    Object.keys(item).forEach(function (key) { localized[key] = item[key]; });
    localized.title = copy[0];
    localized.description = copy[1];
    return localized;
  }

  window.FranklinMenuI18n = Object.freeze({
    apply: apply,
    t: t,
    normalize: normalize,
    register: register,
    extend: extend,
    getLocale: function () { return currentLocale; },
    localizeMission: localizeMission,
    supported: supportedLocales
  });
}());

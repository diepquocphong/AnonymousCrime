(function () {
  "use strict";

  var api = window.FranklinMenuI18n;
  if (!api || typeof api.extend !== "function") return;

  var entries = {
    vi: {
      "main.online": "CHƠI ONLINE", "online.kicker": "NGƯỜI CHƠI ONLINE", "online.title": "TÌM NGƯỜI CHƠI",
      "online.maxWait": "THỜI GIAN TỐI ĐA", "online.simulation": "MÔ PHỎNG", "online.searching": "ĐANG TÌM PHÒNG...",
      "online.searchingHint": "Đang quét các phiên chơi phù hợp trong khu vực", "online.found": "ĐÃ TẠO NHÓM MÔ PHỎNG",
      "online.foundHint": "Nhóm mô phỏng đã sẵn sàng", "online.cancelled": "ĐÃ DỪNG TÌM KIẾM",
      "online.cancelledHint": "Bạn có thể thử tìm lại bất cứ lúc nào", "online.elapsed": "ĐÃ CHỜ", "online.room": "PHÒNG CHỜ",
      "online.players": "NGƯỜI CHƠI", "online.you": "BẠN", "online.youReady": "BẠN • SẴN SÀNG",
      "online.botReady": "BOT MÔ PHỎNG • SẴN SÀNG", "online.waiting": "ĐANG TÌM...", "online.emptySlot": "Vị trí đang tìm",
      "online.cancel": "HỦY", "online.back": "QUAY LẠI", "online.retry": "TÌM LẠI",
      "online.disclaimer": "Mô phỏng cục bộ • Chưa kết nối máy chủ hoặc người chơi thật",
      "online.progressText": "Đã chờ {elapsed} trên tối đa {max} giây", "a11y.onlineLimit": "Thời gian tìm tối đa 30 giây",
      "a11y.onlineProgress": "Tiến trình tìm người chơi", "a11y.onlinePlayers": "Danh sách người chơi mô phỏng"
    },
    en: {
      "main.online": "PLAY ONLINE", "online.kicker": "ONLINE PLAYERS", "online.title": "FIND PLAYERS",
      "online.maxWait": "MAXIMUM WAIT", "online.simulation": "SIMULATION", "online.searching": "SEARCHING FOR A LOBBY...",
      "online.searchingHint": "Scanning for suitable sessions in your region", "online.found": "SIMULATED SQUAD CREATED",
      "online.foundHint": "The simulated squad is ready", "online.cancelled": "SEARCH STOPPED",
      "online.cancelledHint": "You can search again at any time", "online.elapsed": "WAITED", "online.room": "LOBBY",
      "online.players": "PLAYERS", "online.you": "YOU", "online.youReady": "YOU • READY",
      "online.botReady": "SIMULATED BOT • READY", "online.waiting": "SEARCHING...", "online.emptySlot": "Searching slot",
      "online.cancel": "CANCEL", "online.back": "BACK", "online.retry": "SEARCH AGAIN",
      "online.disclaimer": "Local interface simulation • No real server or players are connected",
      "online.progressText": "Elapsed: {elapsed} of {max} seconds", "a11y.onlineLimit": "Maximum search time is 30 seconds",
      "a11y.onlineProgress": "Player search progress", "a11y.onlinePlayers": "Simulated player list"
    },
    de: {
      "main.online": "ONLINE SPIELEN", "online.kicker": "ONLINE-SPIELER", "online.title": "SPIELER SUCHEN",
      "online.maxWait": "MAXIMALE WARTEZEIT", "online.simulation": "SIMULATION", "online.searching": "LOBBY WIRD GESUCHT...",
      "online.searchingHint": "Passende Sitzungen in deiner Region werden gesucht", "online.found": "SIM-TEAM BEREIT",
      "online.foundHint": "Das simulierte Team ist bereit", "online.cancelled": "SUCHE BEENDET",
      "online.cancelledHint": "Du kannst jederzeit erneut suchen", "online.elapsed": "GEWARTET", "online.room": "LOBBY",
      "online.players": "SPIELER", "online.you": "DU", "online.youReady": "DU • BEREIT",
      "online.botReady": "SIM-BOT • BEREIT", "online.waiting": "SUCHE...", "online.emptySlot": "Freier Suchplatz",
      "online.cancel": "ABBRECHEN", "online.back": "ZURÜCK", "online.retry": "ERNEUT SUCHEN",
      "online.disclaimer": "Lokale Oberflächen-Simulation • Keine Verbindung zu echten Servern oder Spielern",
      "online.progressText": "{elapsed} von höchstens {max} Sekunden gewartet", "a11y.onlineLimit": "Maximale Suchzeit: 30 Sekunden",
      "a11y.onlineProgress": "Fortschritt der Spielersuche", "a11y.onlinePlayers": "Liste simulierter Spieler"
    },
    fr: {
      "main.online": "JOUER EN LIGNE", "online.kicker": "JOUEURS EN LIGNE", "online.title": "CHERCHER DES JOUEURS",
      "online.maxWait": "ATTENTE MAXIMALE", "online.simulation": "SIMULATION", "online.searching": "RECHERCHE D'UN SALON...",
      "online.searchingHint": "Recherche de sessions adaptées dans votre région", "online.found": "ÉQUIPE SIMULÉE CRÉÉE",
      "online.foundHint": "L'équipe simulée est prête", "online.cancelled": "RECHERCHE ARRÊTÉE",
      "online.cancelledHint": "Vous pouvez relancer la recherche à tout moment", "online.elapsed": "ATTENTE", "online.room": "SALON",
      "online.players": "JOUEURS", "online.you": "VOUS", "online.youReady": "VOUS • PRÊT",
      "online.botReady": "BOT SIMULÉ • PRÊT", "online.waiting": "RECHERCHE...", "online.emptySlot": "Place en recherche",
      "online.cancel": "ANNULER", "online.back": "RETOUR", "online.retry": "RECHERCHER",
      "online.disclaimer": "Simulation locale de l'interface • Aucun serveur ni joueur réel connecté",
      "online.progressText": "Temps écoulé : {elapsed} sur {max} secondes", "a11y.onlineLimit": "Temps de recherche maximal de 30 secondes",
      "a11y.onlineProgress": "Progression de la recherche de joueurs", "a11y.onlinePlayers": "Liste de joueurs simulés"
    },
    ja: {
      "main.online": "オンラインプレイ", "online.kicker": "オンラインプレイヤー", "online.title": "プレイヤーを検索",
      "online.maxWait": "最大待機時間", "online.simulation": "シミュレーション", "online.searching": "ロビーを検索中...",
      "online.searchingHint": "地域内の適切なセッションを検索しています", "online.found": "模擬チームを作成しました",
      "online.foundHint": "模擬チームの準備ができました", "online.cancelled": "検索を停止しました",
      "online.cancelledHint": "いつでも再検索できます", "online.elapsed": "待機時間", "online.room": "ロビー",
      "online.players": "プレイヤー", "online.you": "あなた", "online.youReady": "あなた • 準備完了",
      "online.botReady": "模擬BOT • 準備完了", "online.waiting": "検索中...", "online.emptySlot": "検索中の枠",
      "online.cancel": "キャンセル", "online.back": "戻る", "online.retry": "再検索",
      "online.disclaimer": "ローカルUIシミュレーション • 実際のサーバーやプレイヤーには接続していません",
      "online.progressText": "最大{max}秒のうち{elapsed}秒待機", "a11y.onlineLimit": "最大検索時間は30秒です",
      "a11y.onlineProgress": "プレイヤー検索の進行状況", "a11y.onlinePlayers": "模擬プレイヤー一覧"
    },
    ko: {
      "main.online": "온라인 플레이", "online.kicker": "온라인 플레이어", "online.title": "플레이어 찾기",
      "online.maxWait": "최대 대기 시간", "online.simulation": "시뮬레이션", "online.searching": "로비 검색 중...",
      "online.searchingHint": "지역에 맞는 세션을 검색하고 있습니다", "online.found": "모의 팀 생성 완료",
      "online.foundHint": "모의 팀이 준비되었습니다", "online.cancelled": "검색 중지됨",
      "online.cancelledHint": "언제든지 다시 검색할 수 있습니다", "online.elapsed": "대기 시간", "online.room": "대기실",
      "online.players": "플레이어", "online.you": "나", "online.youReady": "나 • 준비 완료",
      "online.botReady": "모의 봇 • 준비 완료", "online.waiting": "검색 중...", "online.emptySlot": "검색 중인 슬롯",
      "online.cancel": "취소", "online.back": "뒤로", "online.retry": "다시 검색",
      "online.disclaimer": "로컬 UI 시뮬레이션 • 실제 서버나 플레이어에 연결되지 않음",
      "online.progressText": "최대 {max}초 중 {elapsed}초 대기", "a11y.onlineLimit": "최대 검색 시간 30초",
      "a11y.onlineProgress": "플레이어 검색 진행률", "a11y.onlinePlayers": "모의 플레이어 목록"
    },
    "zh-Hans": {
      "main.online": "在线游戏", "online.kicker": "在线玩家", "online.title": "搜索玩家",
      "online.maxWait": "最长等待", "online.simulation": "模拟", "online.searching": "正在搜索大厅...",
      "online.searchingHint": "正在扫描所在地区的合适会话", "online.found": "模拟队伍已建立",
      "online.foundHint": "模拟队伍已准备就绪", "online.cancelled": "搜索已停止",
      "online.cancelledHint": "你可以随时重新搜索", "online.elapsed": "已等待", "online.room": "大厅",
      "online.players": "玩家", "online.you": "你", "online.youReady": "你 • 已准备",
      "online.botReady": "模拟机器人 • 已准备", "online.waiting": "搜索中...", "online.emptySlot": "正在搜索的位置",
      "online.cancel": "取消", "online.back": "返回", "online.retry": "重新搜索",
      "online.disclaimer": "本地界面模拟 • 未连接真实服务器或玩家",
      "online.progressText": "最多{max}秒，已等待{elapsed}秒", "a11y.onlineLimit": "最长搜索时间为30秒",
      "a11y.onlineProgress": "玩家搜索进度", "a11y.onlinePlayers": "模拟玩家列表"
    },
    "zh-Hant": {
      "main.online": "線上遊玩", "online.kicker": "線上玩家", "online.title": "搜尋玩家",
      "online.maxWait": "最長等待", "online.simulation": "模擬", "online.searching": "正在搜尋大廳...",
      "online.searchingHint": "正在掃描所在地區的合適連線", "online.found": "模擬隊伍已建立",
      "online.foundHint": "模擬隊伍已準備就緒", "online.cancelled": "搜尋已停止",
      "online.cancelledHint": "你可以隨時重新搜尋", "online.elapsed": "已等待", "online.room": "大廳",
      "online.players": "玩家", "online.you": "你", "online.youReady": "你 • 已準備",
      "online.botReady": "模擬機器人 • 已準備", "online.waiting": "搜尋中...", "online.emptySlot": "正在搜尋的位置",
      "online.cancel": "取消", "online.back": "返回", "online.retry": "重新搜尋",
      "online.disclaimer": "本機介面模擬 • 未連接真實伺服器或玩家",
      "online.progressText": "最多{max}秒，已等待{elapsed}秒", "a11y.onlineLimit": "最長搜尋時間為30秒",
      "a11y.onlineProgress": "玩家搜尋進度", "a11y.onlinePlayers": "模擬玩家清單"
    },
    ru: {
      "main.online": "ИГРАТЬ ОНЛАЙН", "online.kicker": "ИГРОКИ ОНЛАЙН", "online.title": "ПОИСК ИГРОКОВ",
      "online.maxWait": "МАКС. ОЖИДАНИЕ", "online.simulation": "СИМУЛЯЦИЯ", "online.searching": "ПОИСК ЛОББИ...",
      "online.searchingHint": "Поиск подходящих сессий в вашем регионе", "online.found": "СИМУЛИРОВАННАЯ ГРУППА СОЗДАНА",
      "online.foundHint": "Симулированная группа готова", "online.cancelled": "ПОИСК ОСТАНОВЛЕН",
      "online.cancelledHint": "Поиск можно запустить снова в любое время", "online.elapsed": "ОЖИДАНИЕ", "online.room": "ЛОББИ",
      "online.players": "ИГРОКИ", "online.you": "ВЫ", "online.youReady": "ВЫ • ГОТОВЫ",
      "online.botReady": "СИМУЛИРОВАННЫЙ БОТ • ГОТОВ", "online.waiting": "ПОИСК...", "online.emptySlot": "Поиск игрока",
      "online.cancel": "ОТМЕНА", "online.back": "НАЗАД", "online.retry": "ИСКАТЬ СНОВА",
      "online.disclaimer": "Локальная симуляция интерфейса • Нет подключения к реальным серверам или игрокам",
      "online.progressText": "Ожидание: {elapsed} из {max} секунд", "a11y.onlineLimit": "Максимальное время поиска — 30 секунд",
      "a11y.onlineProgress": "Ход поиска игроков", "a11y.onlinePlayers": "Список симулированных игроков"
    },
    "pt-BR": {
      "main.online": "JOGAR ONLINE", "online.kicker": "JOGADORES ONLINE", "online.title": "BUSCAR JOGADORES",
      "online.maxWait": "ESPERA MÁXIMA", "online.simulation": "SIMULAÇÃO", "online.searching": "BUSCANDO SALA...",
      "online.searchingHint": "Procurando sessões adequadas na sua região", "online.found": "EQUIPE SIMULADA CRIADA",
      "online.foundHint": "A equipe simulada está pronta", "online.cancelled": "BUSCA INTERROMPIDA",
      "online.cancelledHint": "Você pode buscar novamente a qualquer momento", "online.elapsed": "ESPERA", "online.room": "SALA",
      "online.players": "JOGADORES", "online.you": "VOCÊ", "online.youReady": "VOCÊ • PRONTO",
      "online.botReady": "BOT SIMULADO • PRONTO", "online.waiting": "BUSCANDO...", "online.emptySlot": "Vaga em busca",
      "online.cancel": "CANCELAR", "online.back": "VOLTAR", "online.retry": "BUSCAR DE NOVO",
      "online.disclaimer": "Simulação local da interface • Sem conexão com servidores ou jogadores reais",
      "online.progressText": "Esperou {elapsed} de no máximo {max} segundos", "a11y.onlineLimit": "Tempo máximo de busca: 30 segundos",
      "a11y.onlineProgress": "Progresso da busca de jogadores", "a11y.onlinePlayers": "Lista de jogadores simulados"
    },
    "es-419": {
      "main.online": "JUGAR EN LÍNEA", "online.kicker": "JUGADORES EN LÍNEA", "online.title": "BUSCAR JUGADORES",
      "online.maxWait": "ESPERA MÁXIMA", "online.simulation": "SIMULACIÓN", "online.searching": "BUSCANDO SALA...",
      "online.searchingHint": "Buscando sesiones adecuadas en tu región", "online.found": "EQUIPO SIMULADO CREADO",
      "online.foundHint": "El equipo simulado está listo", "online.cancelled": "BÚSQUEDA DETENIDA",
      "online.cancelledHint": "Puedes volver a buscar en cualquier momento", "online.elapsed": "ESPERA", "online.room": "SALA",
      "online.players": "JUGADORES", "online.you": "TÚ", "online.youReady": "TÚ • LISTO",
      "online.botReady": "BOT SIMULADO • LISTO", "online.waiting": "BUSCANDO...", "online.emptySlot": "Espacio en búsqueda",
      "online.cancel": "CANCELAR", "online.back": "VOLVER", "online.retry": "BUSCAR DE NUEVO",
      "online.disclaimer": "Simulación local de la interfaz • Sin conexión a servidores ni jugadores reales",
      "online.progressText": "Espera: {elapsed} de un máximo de {max} segundos", "a11y.onlineLimit": "Tiempo máximo de búsqueda: 30 segundos",
      "a11y.onlineProgress": "Progreso de búsqueda de jugadores", "a11y.onlinePlayers": "Lista de jugadores simulados"
    },
    nl: {
      "main.online": "ONLINE SPELEN", "online.kicker": "ONLINE SPELERS", "online.title": "SPELERS ZOEKEN",
      "online.maxWait": "MAXIMALE WACHTTIJD", "online.simulation": "SIMULATIE", "online.searching": "LOBBY ZOEKEN...",
      "online.searchingHint": "Geschikte sessies in je regio worden gezocht", "online.found": "GESIMULEERD TEAM GEMAAKT",
      "online.foundHint": "Het gesimuleerde team is gereed", "online.cancelled": "ZOEKEN GESTOPT",
      "online.cancelledHint": "Je kunt op elk moment opnieuw zoeken", "online.elapsed": "GEWACHT", "online.room": "LOBBY",
      "online.players": "SPELERS", "online.you": "JIJ", "online.youReady": "JIJ • GEREED",
      "online.botReady": "GESIMULEERDE BOT • GEREED", "online.waiting": "ZOEKEN...", "online.emptySlot": "Zoekend vak",
      "online.cancel": "ANNULEREN", "online.back": "TERUG", "online.retry": "OPNIEUW ZOEKEN",
      "online.disclaimer": "Lokale interfacesimulatie • Geen verbinding met echte servers of spelers",
      "online.progressText": "{elapsed} van maximaal {max} seconden gewacht", "a11y.onlineLimit": "Maximale zoektijd: 30 seconden",
      "a11y.onlineProgress": "Voortgang van spelers zoeken", "a11y.onlinePlayers": "Lijst met gesimuleerde spelers"
    }
  };

  Object.keys(entries).forEach(function (locale) {
    api.extend(locale, entries[locale]);
  });
}());

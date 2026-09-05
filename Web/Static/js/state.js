// state — 由 HtmlUI 单体拆分（原 app.js）
// ===== 全局状态（所有面板模块共享） =====

let chests = [];
let items = [];
let dragonItems = [];
let dragonTypes = [];
let dragonEntities = [];
let dragonNatures = [];
let autoRefresh = true;
let selectedChest = -1;
let showDragonSouls = false;
let searchQuery = '';
let categoryOpen = false;
let dragonMainOpen = false;
let dragonView = ''; // 'souls' | 'materials' | 'summon' | ''
let dragonSouls = [];
let npcMainOpen = false;
let npcPanelOpen = false;
let npcListData = [];
let techTreeOpen = false;
let techTreeData = null;
let npcView = ''; // 'explore' | 'entities' | ''
let entityEditorData = [];
let filters = [];



// 外置数据表（/static/data/*.json，init 时加载）
var NPC_TYPES = {};
var TECH_TREE = [];
var TECH_INFO = {};
var TECH_ICON = {};

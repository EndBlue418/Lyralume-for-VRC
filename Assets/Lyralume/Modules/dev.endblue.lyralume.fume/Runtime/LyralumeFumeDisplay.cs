using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDK3.Data;

// Floating article prototype inspired by Folia Fume. Modified 2026-10-14.
// AGPL-3.0; see Documentation/ for license and attribution. Playback remains owned by VizVid.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeFumeDisplay : UdonSharpBehaviour
{
    public LyralumePlaybackState core;
    public LyralumeController lyrics;
    public LyralumeScreenOutput screenOutput;
    public bool visualEnabled = true;
    public bool restrictToNetEase = true;
    public bool keywordColors = true;
    public bool coverColors = true;
    public bool audioEffects = true;
    [Range(0,1)] public float singingColorStrength = 0.78f;
    public Camera stageCamera;
    public RenderTexture output;
    public Renderer[] screens;
    public Material[] screenMaterials;
    public Material[] originalMaterials;
    public TMP_Text[] legacyTexts = new TMP_Text[0];
    public MeshFilter[] lineMeshes;
    public Material glyphMaterial;
    public Material backgroundMaterial;
    public AudioLink.AudioLink backgroundAudioLink;
    public LyralumeAudioAnalysis localAudio;
    public MeshFilter statusMesh;
    public TMP_Text statusText;
    public TMP_Text subtitleText;
    public MeshFilter translationMesh;
    public MeshFilter romanizationMesh;
    public Material secondaryMaterial;
    [Header("Optional LTCGI output (existing VizVid CRT)")]
    public CustomRenderTexture ltcgiOutput;
    public Material ltcgiCompositeMaterial;
    private Material ltcgiVideoMaterial;
    private bool ltcgiInitialized;
    private int overviewRevision;
    private bool buildingNativeOverview;
    private Mesh nativeOverviewScratch;
    [HideInInspector] public string displayedTranslation = "", displayedRomanization = "";
    private Mesh translationGeometry, romanizationGeometry;
    public Transform articleRoot;
    public VRC.Udon.UdonBehaviour modeSwitch;
    [HideInInspector] public int displayMode = 1;
    public bool diagnostics = false;
    [HideInInspector] public DataDictionary coverData;
    public void ReceivePendingCoverData() { ReceiveCoverData(coverData); }
    [Header("Editor-baked glyphs from the existing world font")]
    public int[] glyphCodes;
    public Vector4[] glyphUVs;
    public Vector4[] glyphMetrics;
    public float[] glyphAdvances;
    [Header("Layout (legacy character limit retained for scene compatibility)")]
    public int maxCharactersPerLine = 220;
    public float columnWidth = 18f;
    public float rowHeight = 1.8f;
    public float normalFontSize = 0.48f;
    public float heroFontSize = 0.9f;
    [Range(0.1f, 1f)] public float wrappedMotionScale = 0.5f;
    [Range(0, 1)] public float readingStability = 0.6f;
    [Header("Explicit keyword palette (1 purple, 2 red, 3 green, 4 pink, 5 orange, 6 blue, 7 night, 8 gold, 9 dream, 10 gray, 11 fear, 12 uncertainty, 13 calm, 14 guilt, 15 envy, 16 brown, 17 white, 18 black, 19 silver, 20 yellow, 21 cyan)")]
    public string[] keywordTerms = { "cry", "crying", "cried", "fake", "solve", "tears", "tear", "love", "loved", "fire", "rain", "night", "light", "shine", "dream", "dreams", "涙", "嘘", "炎", "真夜中" };
    public int[] keywordPalette = { 1, 1, 1, 2, 3, 1, 1, 4, 4, 5, 6, 7, 8, 8, 9, 9, 1, 2, 5, 7 };
    // Versioned additions also reach scenes that retain older private Udon data.
    // Equal-length custom matches above retain priority over these built-in defaults.
    private string[] expandedKeywordTerms;
    private int[] expandedKeywordPalette;
    private int expandedKeywordVersion;
    private void EnsureExpandedKeywordTable()
    {
        // Increment the version for every vocabulary/palette edit, including same-length edits.
        if (expandedKeywordVersion == 7 && expandedKeywordTerms != null && expandedKeywordTerms.Length == 2928 && expandedKeywordPalette != null && expandedKeywordPalette.Length == 2928) return;
        expandedKeywordTerms = new string[] {
        "cries", "weep", "weeping", "sorrow", "sadness", "lonely", "loneliness", "heartbroken", "眼淚", "眼泪", "哭泣", "悲しみ",
        "孤独", "ache", "aching", "heartache", "heartbreak", "broken", "shattered", "hurt", "hurting", "pain", "painful", "wounded",
        "wounds", "grief", "grieving", "mourn", "mourning", "misery", "miserable", "despair", "desperate", "hopeless", "emptiness", "empty",
        "solitude", "sorrowful", "tearful", "wept", "心碎", "心痛", "傷痛", "伤痛", "悲傷", "悲伤", "悲哀", "哀傷",
        "哀伤", "痛苦", "孤單", "孤单", "孤獨", "哀愁", "絶望", "寂しさ", "切なさ", "悲しさ", "失恋", "喪失",
        "aches", "ached", "heartaches", "heartbreaking", "heartbreaks", "breakup", "breakups", "shatter", "shatters", "wound", "wounding", "suffer",
        "suffers", "suffered", "suffering", "sorrows", "mourned", "grieved", "tearfully", "helpless", "helplessness", "regret", "regrets", "regretting",
        "goodbye", "goodbyes", "farewell", "farewells", "abandoned", "abandonment", "寂寞", "失落", "難過", "难过", "遺憾", "遗憾",
        "離別", "离别", "離開", "离开", "告別", "告别", "心酸", "心疼", "破碎", "淚水", "泪水", "苦しみ",
        "別れ", "後悔", "孤立", "ため息", "lie", "lies", "liar", "liars", "lying", "fakes", "faked", "betrayal",
        "betrayed", "謊言", "谎言", "虛假", "虚假", "偽物", "hatred", "hate", "hating", "hated", "anger", "angry",
        "rage", "furious", "blood", "bloody", "scar", "scars", "devil", "demon", "demons", "poison", "poisoned", "sinner",
        "sin", "sins", "traitor", "deceit", "deceived", "deceiving", "deception", "pretend", "pretending", "pretended", "背叛", "欺瞞",
        "欺瞒", "偽裝", "伪装", "仇恨", "憤怒", "愤怒", "鮮血", "鲜血", "傷痕", "伤痕", "罪悪", "憎しみ",
        "怒り", "裏切り", "毒薬", "betray", "betrays", "betraying", "liars'", "deceive", "deceiver", "deceivers", "dishonest", "dishonesty",
        "cheat", "cheats", "cheated", "cheating", "faking", "venom", "venomous", "toxic", "toxicity", "hell", "hellfire", "evil",
        "wicked", "cruel", "cruelty", "sinners", "sinful", "bleed", "bleeds", "bleeding", "bled", "背信", "背棄", "背弃",
        "欺詐", "欺诈", "騙局", "骗局", "假面", "偽善", "伪善", "憎恨", "怒火", "血液", "悪魔", "地獄",
        "仮面", "偽り", "憎悪", "solved", "solving", "alive", "bloom", "blooms", "blooming", "reborn", "新生", "綻放",
        "绽放", "hope", "hopes", "hopeful", "hoping", "heal", "heals", "healing", "healed", "breathe", "breathing", "breath",
        "survive", "surviving", "survived", "survivor", "rise", "rising", "risen", "grow", "growing", "growth", "spring", "garden",
        "gardens", "leaf", "leaves", "forest", "forests", "emerald", "renewal", "renew", "awaken", "awakening", "希望", "盼望",
        "治癒", "治愈", "復甦", "复苏", "甦醒", "苏醒", "呼吸", "生命", "森林", "春天", "若葉", "新緑",
        "芽吹き", "再生", "healer", "healers", "survives", "survivors", "breaths", "breathed", "recovery", "recover", "recovered", "recovering",
        "revive", "revived", "reviving", "rebirth", "resurrection", "freedom", "free", "liberty", "liberation", "escape", "escaped", "escaping",
        "courage", "brave", "bravery", "faith", "faithful", "trust", "trusting", "seed", "seeds", "sprout", "sprouts", "sprouting",
        "green", "evergreen", "拯救", "救贖", "救赎", "復活", "复活", "重生", "自由", "勇氣", "勇气", "信念",
        "信仰", "生機", "生机", "青葉", "勇気", "息吹", "救い", "loving", "lover", "lovers", "kiss", "kisses",
        "kissed", "darling", "romance", "愛情", "爱情", "戀人", "恋人", "口づけ", "beloved", "sweetheart", "sweethearts", "honey",
        "affection", "desire", "desires", "desired", "adore", "adoring", "adored", "devotion", "devoted", "embrace", "embraces", "embraced",
        "hug", "hugs", "hugged", "caress", "caresses", "passion", "passionate", "tender", "tenderness", "cherish", "cherished", "戀愛",
        "恋爱", "愛戀", "爱恋", "深愛", "深爱", "溫柔", "温柔", "擁抱", "拥抱", "親吻", "亲吻", "心動",
        "心动", "眷戀", "眷恋", "愛おしさ", "恋心", "接吻", "babe", "baby", "babies", "babygirl", "boyfriend", "girlfriend",
        "fiance", "fiancee", "wedding", "marry", "married", "marriage", "bride", "groom", "lovely", "lovable", "loveless", "lovesick",
        "lovestruck", "romantic", "romantically", "adoration", "adores", "cherishes", "cherishing", "embracing", "hugging", "kissing", "caressing", "temptation",
        "tempted", "親愛", "亲爱", "寶貝", "宝贝", "甜蜜", "熱戀", "热恋", "相愛", "相爱", "初戀", "初恋",
        "戀慕", "恋慕", "相思", "誓約", "誓约", "恋人たち", "花嫁", "抱擁", "flame", "flames", "burning", "burned",
        "burnt", "blaze", "火焰", "燃燒", "燃烧", "burn", "burns", "blazing", "blazed", "ignite", "ignites", "ignited",
        "ignition", "spark", "sparks", "sparking", "flare", "flares", "flaring", "wildfire", "wildfires", "inferno", "heat", "heated",
        "hot", "lightning", "thunder", "thunderstorm", "explosion", "explosions", "explosive", "phoenix", "烈火", "火花", "火光", "灼熱",
        "灼热", "熾熱", "炽热", "閃電", "闪电", "雷鳴", "雷鸣", "稲妻", "熱情", "fired", "fiery", "flaming",
        "burner", "burners", "igniting", "sparked", "sparkling fire", "flareup", "flared", "scorch", "scorched", "scorching", "ablaze", "eruption",
        "erupting", "volcano", "volcanic", "lava", "furnace", "embers", "ember", "ashes", "ash", "smoke", "smoking", "smoky",
        "electric", "electricity", "electrified", "exploding", "exploded", "爆炸", "爆發", "爆发", "燃盡", "燃尽", "灰燼", "灰烬",
        "餘燼", "余烬", "烈焰", "烈日", "熱浪", "热浪", "火山", "溶岩", "残り火", "灼熱地獄", "rains", "raining",
        "rainy", "ocean", "oceans", "sea", "waves", "blue", "海洋", "藍色", "蓝色", "雨音", "wave", "water",
        "waters", "waterfall", "waterfalls", "river", "rivers", "lake", "lakes", "tide", "tides", "storm", "storms", "rainstorm",
        "raindrop", "raindrops", "rainfall", "drizzle", "snow", "snowy", "snowfall", "snowflake", "snowflakes", "ice", "icy", "cold",
        "colder", "coldest", "frozen", "freeze", "freezing", "frost", "winter", "winters", "azure", "sapphire", "雨滴", "雨水",
        "雨中", "暴雨", "大雨", "細雨", "细雨", "河流", "潮汐", "浪花", "海浪", "冰冷", "冰雪", "凍結",
        "冻结", "雪花", "冬天", "雪景色", "吹雪", "氷雨", "水面", "rained", "drizzling", "stormy", "storming", "stormswept",
        "hurricane", "hurricanes", "typhoon", "typhoons", "tornado", "tornadoes", "flood", "floods", "flooded", "flooding", "drown", "drowning",
        "drowned", "sinking", "sink", "sinks", "underwater", "deep sea", "seaside", "seashore", "shore", "shores", "coast", "coastal",
        "stream", "streams", "creek", "creeks", "blizzard", "blizzards", "snowing", "snowed", "frozen heart", "crimson rain", "淋雨", "雨季",
        "雨夜", "冷雨", "驟雨", "骤雨", "霧雨", "雾雨", "風暴", "风暴", "暴風雨", "暴风雨", "淹沒", "淹没",
        "沉沒", "沉没", "海岸", "深海", "海底", "寒冬", "寒冷", "冰霜", "冷たい雨", "雨上がり", "粉雪", "雪明かり",
        "nights", "midnight", "darkness", "shadow", "shadows", "黑夜", "午夜", "黑暗", "暗闇", "夜空", "dark", "darker",
        "darkest", "shadowed", "twilight", "dusk", "moon", "moonlight", "moonlit", "lunar", "eclipse", "eclipses", "blackout", "abyss",
        "silence", "silent", "silently", "ghost", "ghosts", "haunted", "haunting", "nightmare", "nightmares", "insomnia", "sleepless", "sleep",
        "sleeping", "asleep", "月光", "月色", "月夜", "夜晚", "夜裡", "夜里", "黃昏", "黄昏", "暮色", "寂靜",
        "寂静", "沉默", "深夜", "月明かり", "夕暮れ", "静寂", "悪夢", "幽霊", "nocturnal", "moonrise", "moonbeam", "moonbeams",
        "moons", "crescent", "sundown", "sunset", "sunsets", "nightfall", "nighttime", "nightshade", "shadowy", "shadowing", "silenced", "ghostly",
        "phantom", "phantoms", "haunt", "haunts", "midnights", "sleeplessness", "slumber", "slumbering", "lullaby", "lullabies", "darkened", "darkening",
        "oblivion", "亡靈", "亡灵", "幽魂", "幽暗", "暗影", "暗夜", "長夜", "长夜", "漆黑", "無聲", "无声",
        "夢魘", "梦魇", "夜風", "夜风", "夜雨", "月影", "月亮", "夕陽", "夕阳", "夕闇", "黄昏時", "満月",
        "三日月", "夜想曲", "眠り", "lights", "shining", "sunlight", "sunshine", "starlight", "glow", "glowing", "golden", "光芒",
        "閃耀", "闪耀", "陽光", "阳光", "輝き", "shone", "shined", "luminous", "illuminate", "illuminated", "illuminating", "radiance",
        "radiant", "bright", "brighter", "brightest", "brightness", "sun", "sunny", "sunrise", "dawn", "daybreak", "daylight", "star",
        "stars", "starry", "glitter", "glitters", "glittering", "sparkle", "sparkles", "sparkling", "gleam", "gleaming", "gold", "halo",
        "halos", "angel", "angels", "heaven", "heavenly", "joy", "joyful", "happiness", "happy", "blessed", "blessing", "blessings",
        "glory", "glorious", "treasure", "treasures", "黎明", "晨光", "曙光", "日出", "星辰", "星空", "星河", "星輝",
        "星辉", "燦爛", "灿烂", "光輝", "光辉", "璀璨", "幸福", "快樂", "快乐", "天使", "祝福", "寶藏",
        "宝藏", "夜明け", "暁", "星屑", "煌めき", "木漏れ日", "幸せ", "天国", "shines", "glistening", "glisten", "glistens",
        "glittered", "sparkled", "sparkling stars", "shimmer", "shimmers", "shimmering", "shimmered", "twinkle", "twinkles", "twinkling", "twinkled", "dazzle",
        "dazzling", "dazzled", "brilliant", "brilliance", "radiate", "radiates", "radiating", "radiated", "enlighten", "enlightened", "sunbeam", "sunbeams",
        "sunlit", "skylight", "skylights", "constellation", "constellations", "galaxy", "galaxies", "comet", "comets", "meteor", "meteors", "aurora",
        "rainbow", "rainbows", "曙色", "朝陽", "朝阳", "暖陽", "暖阳", "流星", "極光", "极光", "彩虹", "繁星",
        "群星", "光明", "明亮", "耀眼", "光彩", "晨曦", "栄光", "星座", "流れ星", "陽だまり", "輝石", "dreaming",
        "dreamed", "dreamt", "fantasy", "fantasies", "夢境", "梦境", "夢想", "梦想", "夢物語", "dreamer", "dreamers", "daydream",
        "daydreams", "daydreaming", "dreamlike", "wish", "wishes", "wished", "wishing", "magic", "magical", "miracle", "miracles", "wonder",
        "wonders", "wonderful", "imagine", "imagining", "imagined", "imagination", "illusion", "illusions", "memory", "memories", "remember", "remembered",
        "remembering", "nostalgia", "nostalgic", "yesterday", "yesterdays", "eternity", "eternal", "eternally", "paradise", "fairytale", "fairytales", "fairy",
        "fairies", "回憶", "回忆", "記憶", "记忆", "思念", "想念", "懷念", "怀念", "幻想", "幻影", "幻覺",
        "幻觉", "魔法", "奇蹟", "奇迹", "願望", "愿望", "童話", "童话", "永恆", "永恒", "過往", "过往",
        "昨日", "思い出", "憧れ", "面影", "幻覚", "奇跡", "願い", "追憶", "daydreamed", "daydreamer", "daydreamers", "dreamt of",
        "dreamland", "dreamlands", "dreamworld", "dreamworlds", "dreamless", "wishful", "wisher", "wishers", "wishing well", "enchantment", "enchanted", "enchanting",
        "spellbound", "spell", "spells", "sorcery", "wizard", "wizards", "witch", "witches", "mystic", "mystical", "mystery", "mysterious",
        "reminisce", "reminiscing", "reminiscence", "reminiscences", "recollection", "recollections", "memorize", "memorized", "keepsake", "keepsakes", "everlasting", "timeless",
        "infinity", "infinite", "青春", "往事", "舊夢", "旧梦", "美夢", "美梦", "白日夢", "白日梦", "夢幻", "梦幻",
        "追忆", "昔日", "思緒", "思绪", "無限", "无限", "永遠", "永远", "樂園", "乐园", "魔術", "魔术",
        "蜃気楼", "夢見心地", "懐かしさ", "永遠の夢", "hide", "hides", "hiding", "hid", "hidden", "conceal", "conceals", "concealed",
        "concealing", "躲藏", "躲避", "隱藏", "隐藏", "掩飾", "掩饰", "隠す", "隠して", "隠した", "隠され", "隠れる",
        "隠れて", "隠れた", "隠し事", "numb", "numbness", "numbed", "exhausted", "exhaustion", "weary", "weariness", "fatigue", "fatigued",
        "burnout", "listless", "lifeless", "麻木", "疲憊", "疲惫", "倦怠", "疲倦", "筋疲力盡", "筋疲力尽", "無感覚", "疲れ",
        "疲れた", "疲れて", "疲れ果て", "無気力", "anxiety", "anxious", "anxiously", "afraid", "fear", "fears", "feared", "fearful",
        "fearfully", "fearing", "panic", "panicked", "panicking", "terrified", "terrifying", "terror", "dread", "dreading", "uneasy", "unease",
        "insecure", "insecurity", "焦慮", "焦虑", "恐懼", "恐惧", "害怕", "驚慌", "惊慌", "惶恐", "不安", "恐怖",
        "怯え", "怯えて", "怯える", "怖い", "怖くて", "恐れて", "恐れる", "doubt", "doubts", "doubted", "doubting", "doubtful",
        "confused", "confusion", "uncertain", "uncertainty", "unsure", "hesitate", "hesitates", "hesitated", "hesitating", "hesitation", "bewildered", "bewilderment",
        "迷惘", "迷茫", "迷失", "猶豫", "犹豫", "遲疑", "迟疑", "困惑", "自我懷疑", "自我怀疑", "迷い", "迷って",
        "迷った", "迷う", "戸惑い", "戸惑う", "戸惑って", "戸惑った", "ためらい", "ためらう", "躊躇", "calm", "calmer", "calmest",
        "calmly", "calming", "peace", "peaceful", "peacefully", "relief", "relieved", "serene", "serenity", "tranquil", "tranquility", "soothe",
        "soothes", "soothed", "soothing", "reassure", "reassured", "reassuring", "安寧", "安宁", "平靜", "平静", "安心", "安穩",
        "安稳", "釋然", "释然", "寬慰", "宽慰", "安らぎ", "安らか", "穏やか", "落ち着く", "落ち着いて", "安堵", "shame",
        "ashamed", "shameful", "guilt", "guilty", "remorse", "remorseful", "apologize", "apologized", "apologizing", "apologise", "apologised", "apologising",
        "羞愧", "內疚", "内疚", "愧疚", "自責", "自责", "懺悔", "忏悔", "罪悪感", "自責の念", "恥ずかしい", "恥じる",
        "悔やむ", "jealous", "jealousy", "jealously", "envy", "envied", "envious", "enviously", "possessive", "possessiveness", "obsess", "obsessed",
        "obsessing", "obsession", "obsessive", "嫉妒", "妒忌", "忌妒", "佔有慾", "占有欲", "執著", "执着", "執着", "嫉妬",
        "妬み", "妬む", "妬んで", "resolve", "resolved", "resolute", "determination", "determined", "defy", "defies", "defied", "defying",
        "defiance", "defiant", "unbreakable", "unyielding", "決心", "决心", "堅定", "坚定", "不屈", "不服輸", "不服输", "反抗",
        "決意", "決して諦めない", "諦めない", "death", "deaths", "dead", "die", "dies", "died", "dying", "deceased", "lifelessness",
        "funeral", "funerals", "grave", "graves", "graveyard", "graveyards", "cemetery", "coffin", "coffins", "tomb", "tombs", "burial",
        "buried", "死亡", "死去", "死別", "死别", "逝去", "逝世", "亡故", "葬禮", "葬礼", "墓地", "墳墓",
        "坟墓", "棺木", "棺材", "死んだ", "死んで", "死んでる", "死んでいる", "死にたい", "死にたくない", "亡くなった", "亡くなる", "亡くなって",
        "葬式", "葬儀", "お墓", "deep", "deeper", "deepest", "deep down", "deep inside", "deep within", "deep sleep", "depth", "depths",
        "深處", "深处", "深淵", "深渊", "深邃", "幽深", "深い闇", "深い夜", "深い眠り", "心の奥", "deep love", "deep in love",
        "deep affection", "deep sorrow", "deep sadness", "deep pain", "break down", "breaks down", "broke down", "broken down", "breaking down", "break me", "breaking me", "break my heart",
        "breaks my heart", "broke my heart", "breaking my heart", "blues", "pathetic", "unhappy", "unhappiness", "unloved", "unwanted", "unworthy", "worthless", "worthlessness",
        "rejected", "rejection", "dejected", "anguish", "anguished", "torment", "tormented", "agony", "agonizing", "fragments", "fragmented", "frantic",
        "心如刀割", "撕心裂肺", "心力交瘁", "心灰意冷", "心寒", "無助", "无助", "無望", "无望", "絕望", "崩潰", "崩溃",
        "支離破碎", "支离破碎", "失魂落魄", "痛不欲生", "胸が痛い", "胸が苦しい", "心が壊れ", "心が折れ", "報われない", "やるせない", "哀しい", "break free",
        "break me free", "break us free", "breaking free", "broke free", "break away", "breaking away", "set me free", "set us free", "set you free", "set them free", "believe", "believes",
        "believed", "believing", "believer", "believers", "thrive", "thrives", "thrived", "thriving", "awake", "awakes", "awoke", "awoken",
        "awakened", "awakens", "reclaim", "reclaims", "reclaimed", "reclaiming", "unbound", "unchained", "unchain", "unchaining", "掙脫", "挣脱",
        "解放", "解脫", "解脱", "突破", "破繭", "破茧", "浴火重生", "相信", "堅信", "坚信", "甦生", "蘇生",
        "解き放つ", "解き放て", "解き放って", "解き放たれ", "解放され", "解放する", "解放して", "信じる", "信じて", "信じた", "信じたい", "目覚め",
        "立ち上がれ", "立ち上がる", "立ち上がって", "strong", "stronger", "strongest", "strength", "strengthen", "strengthens", "strengthened", "strengthening", "resilience",
        "resilient", "relentless", "fearless", "fearlessly", "undaunted", "unafraid", "eager", "eagerness", "tenacious", "tenacity", "persevere", "perseveres",
        "persevered", "persevering", "perseverance", "堅強", "坚强", "頑強", "顽强", "堅持", "坚持", "奮起", "奋起", "振作",
        "勇往直前", "義無反顧", "义无反顾", "無所畏懼", "无所畏惧", "不會認輸", "不会认输", "強くなれ", "強くなる", "強くなりたい", "負けない", "負けたくない",
        "くじけない", "恐れない", "stuck", "trapped", "trapping", "captive", "captivity", "confine", "confined", "confinement", "imprisoned", "imprisonment",
        "restless", "restlessness", "mess", "messed", "torn", "conflicted", "conflicting", "struggle", "struggles", "struggled", "struggling", "stumble",
        "stumbled", "stumbling", "dead end", "迷路", "困住", "受困", "束縛", "束缚", "囚禁", "掙扎", "挣扎", "糾結",
        "纠结", "進退兩難", "进退两难", "不知所措", "無所適從", "无所适从", "迷子", "囚われ", "閉じ込め", "縛られ", "もがく", "もがいて",
        "葛藤", "行き止まり", "allay", "allays", "allayed", "allaying", "let go", "letting go", "forgive", "forgives", "forgiven", "forgiving",
        "forgiveness", "reconcile", "reconciled", "reconciliation", "comfort", "comforted", "comforting", "solace", "release me", "放下", "放手", "原諒",
        "原谅", "寬恕", "宽恕", "和解", "解開心結", "解开心结", "ほっとする", "ほっとした", "赦し", "許して", "許した", "許せる",
        "drained", "draining me", "drain me", "worn out", "worn down", "burned out", "burnt out", "hollow", "hollowness", "detached", "detachment", "apathetic",
        "apathy", "disappear", "disappears", "disappeared", "disappearing", "vanish", "vanishes", "vanished", "vanishing", "消失", "消散", "空洞",
        "枯竭", "精疲力竭", "身心俱疲", "耗盡", "耗尽", "無動於衷", "无动于衷", "消えて", "消えた", "消えたい", "消える", "消えゆく",
        "虚ろ", "虚無", "燃え尽き", "抜け殻", "haunting fear", "fright", "frightened", "frightening", "horror", "horrified", "tremble", "trembles",
        "trembled", "trembling", "shiver", "shivering", "恐慌", "驚恐", "惊恐", "畏懼", "畏惧", "戰慄", "战栗", "顫抖",
        "颤抖", "震えて", "震える", "震えた", "怯えた", "unforgiven", "unforgivable", "regretful", "repent", "repented", "repentance", "repentant",
        "sorry", "apology", "apologies", "悔恨", "懊悔", "歉意", "羞恥", "羞耻", "悔い", "悔やんで", "悔やんだ", "ごめん",
        "ごめんなさい", "cling", "clings", "clinging", "clung", "crave", "craves", "craved", "craving", "cravings", "渴望", "貪戀",
        "贪恋", "貪婪", "贪婪", "貪念", "佔有", "占有", "独占", "独占欲", "しがみつく", "しがみついて", "手放せない", "alone",
        "lonesome", "lonelier", "loneliest", "sad", "sadly", "saddened", "sadnesses", "unfulfilled", "unbearable", "unbearably", "disappointed", "disappointment",
        "disappointing", "devastated", "devastating", "devastation", "crushed", "crushing", "losing", "lose", "loses", "lost", "loss", "losses",
        "leave", "leaves me", "leaving", "left behind", "left alone", "gone", "gone away", "gone forever", "hurts", "hurtful", "unhurt", "weepingly",
        "weeps", "sobs", "sob", "sobbing", "sobbed", "wail", "wailing", "lament", "lamenting", "lamented", "lamentation", "heartless",
        "heartlessly", "heartlessness", "heartbrokenness", "melancholy", "melancholic", "depression", "depressed", "depressing", "downhearted", "downcast", "desolate", "desolation",
        "forlorn", "bereft", "bereavement", "forsaken", "estranged", "ruined", "ruin", "ruins", "destroyed", "destroy", "destroys", "destroying",
        "fall apart", "falling apart", "fell apart", "fall to pieces", "falling to pieces", "崩塌", "潰敗", "溃败", "毀滅", "毁灭", "毀掉", "毁掉",
        "離去", "离去", "離散", "离散", "離散了", "失去", "失去了", "失望", "悲痛", "悲慟", "悲恸", "悵然",
        "怅然", "惆悵", "惆怅", "淒涼", "凄凉", "淒美", "凄美", "傷感", "伤感", "感傷", "感伤", "傷心",
        "伤心", "淚流", "泪流", "流淚", "流泪", "落淚", "落泪", "落涙", "悲しい", "悲しく", "悲しかった", "悲しんで",
        "悲しむ", "寂しい", "寂しく", "寂しかった", "寂しがり", "淋しい", "淋しく", "苦しい", "苦しく", "苦しかった", "辛い", "辛くて",
        "辛かった", "つらい", "つらくて", "つらかった", "泣きたい", "泣きながら", "泣き出す", "泣き出して", "泣き続け", "失った", "失って", "失う",
        "失いたくない", "失いたくなくて", "壊れた", "壊れて", "壊れる", "壊した", "壊して", "壊す", "傷ついた", "傷ついて", "傷つく", "傷つけた",
        "傷つけて", "傷つける", "ひとりぼっち", "独りぼっち", "一人ぼっち", "miss", "misses", "missed", "missing", "miss you", "missed you", "missing you",
        "miss me", "longing", "longed", "yearn", "yearns", "yearned", "yearning", "wistful", "wistfully", "homesick", "homesickness", "reminiscent",
        "forget", "forgets", "forgot", "forgotten", "forgetting", "unforgettable", "remembering you", "come back", "coming back", "want you back", "再見", "再见",
        "重逢", "相逢", "想你", "想念你", "思慕", "留戀", "留恋", "不捨", "不舍", "依依不捨", "依依不舍", "念念不忘",
        "難忘", "难忘", "忘記", "忘记", "忘不了", "忘不掉", "懐かしい", "懐かしく", "懐かしかった", "恋しい", "恋しく", "恋しかった",
        "会いたい", "逢いたい", "会えない", "逢えない", "忘れない", "忘れられない", "忘れて", "忘れた", "思い出す", "思い出して", "思い出した", "帰りたい",
        "戻りたい", "あの頃", "あの日", "loves", "lovingly", "loveliness", "fond", "fondness", "infatuated", "infatuation", "enamored", "enamoured",
        "adorable", "attracted", "attraction", "intimate", "intimacy", "togetherness", "affectionate", "affectionately", "dearest", "precious", "cherishes you", "need you",
        "need me", "need your love", "want you", "want me", "hold me", "hold you", "hold me close", "hold you close", "stay with me", "stay with you", "fall in love", "falls in love",
        "falling in love", "fell in love", "crazy in love", "madly in love", "love you", "love me", "心愛", "心爱", "摯愛", "挚爱", "珍愛", "珍爱",
        "寵愛", "宠爱", "迷戀", "迷恋", "傾心", "倾心", "鍾情", "钟情", "相伴", "依偎", "纏綿", "缠绵",
        "溫存", "温存", "愛してる", "愛している", "愛して", "愛した", "愛する", "愛され", "愛しい", "愛しく", "愛しかった", "愛おしい",
        "愛おしく", "恋する", "恋した", "恋して", "好きだ", "好きな", "好きで", "好きだった", "大好き", "抱きしめて", "抱きしめた", "抱きしめる",
        "抱きたい", "抱かれて", "キスして", "キスした", "キスする", "そばにいて", "傍にいて", "fight", "fights", "fought", "fighting", "fighter",
        "fighters", "battle", "battles", "battling", "warrior", "warriors", "rebel", "rebels", "rebellion", "rebellious", "resist", "resists",
        "resisted", "resisting", "resistance", "scream", "screams", "screamed", "screaming", "shout", "shouts", "shouted", "shouting", "roar",
        "roars", "roared", "roaring", "unstoppable", "unshakable", "unshakeable", "undefeated", "invincible", "persistent", "persistence", "proud", "pride",
        "confident", "confidence", "bold", "boldness", "daring", "ambition", "ambitious", "triumph", "triumphant", "victory", "victorious", "win",
        "wins", "won", "winning", "winner", "winners", "戰鬥", "战斗", "戰勝", "战胜", "抵抗", "反擊", "反击",
        "逆襲", "逆袭", "不甘", "不甘心", "吶喊", "呐喊", "嘶吼", "咆哮", "怒吼", "頑抗", "顽抗", "無畏",
        "无畏", "驕傲", "骄傲", "自信", "堅毅", "坚毅", "強韌", "强韧", "戦う", "戦って", "戦った", "闘う",
        "闘って", "闘った", "叫ぶ", "叫んで", "叫んだ", "抗う", "抗って", "抗った", "立ち向かう", "立ち向かって", "諦めず", "諦めたくない",
        "負けず", "負けられない", "譲れない", "揺るがない", "誇り", "誇らしい", "save", "saves", "saved", "saving", "savior", "saviour",
        "redeem", "redeemed", "redeeming", "redemption", "rescue", "rescues", "rescued", "rescuing", "flourish", "flourishes", "flourishing", "fly",
        "flies", "flew", "flown", "flying", "soar", "soars", "soared", "soaring", "wings", "wing", "unbroken", "promise",
        "promises", "promised", "promising", "optimism", "optimistic", "possibility", "possibilities", "tomorrow", "future", "futures", "new beginning", "new beginnings",
        "start again", "重新開始", "重新开始", "從頭再來", "从头再来", "未來", "未来", "明天", "翱翔", "飛翔", "飞翔", "飛揚",
        "飞扬", "振翅", "展翅", "救助", "救援", "救命", "約定", "约定", "承諾", "承诺", "救って", "救われ",
        "救いたい", "助けて", "助けたい", "助かった", "飛べる", "飛びたい", "飛び立つ", "飛び立って", "羽ばたく", "羽ばたいて", "羽ばたけ", "やり直す",
        "やり直せる", "始まり", "始まる", "始めよう", "明日へ", "信じ続け", "faint", "fainted", "fainting", "faded", "fading", "fade away",
        "fades away", "fading away", "fade out", "faded out", "wasted", "wasting away", "wither", "withers", "withered", "withering", "bored", "boredom",
        "indifferent", "indifference", "emotionless", "soulless", "colorless", "colourless", "gray", "grey", "grayscale", "greyscale", "killed", "dying inside",
        "dead inside", "沉寂", "沉睡", "凋零", "凋謝", "凋谢", "枯萎", "枯槁", "灰色", "灰白", "死寂", "毫無感覺",
        "毫无感觉", "無所謂", "无所谓", "心如死灰", "漸漸消失", "渐渐消失", "薄れて", "薄れた", "薄れる", "色褪せ", "色あせ", "枯れて",
        "枯れた", "枯れる", "灰になって", "灰になった", "何も感じない", "どうでもいい", "scared", "scare", "scares", "scaring", "alarmed", "alarming",
        "apprehensive", "apprehension", "distress", "distressed", "distressing", "nervous", "nervously", "nervousness", "paranoid", "paranoia", "hysterical", "hysteria",
        "petrified", "petrifying", "frightfully", "膽怯", "胆怯", "怯懦", "忐忑", "心慌", "惴惴不安", "提心吊膽", "提心吊胆", "驚悚",
        "惊悚", "怕失去", "怕失去你", "恐ろしい", "恐ろしく", "怖かった", "怖くなる", "怖がる", "怖がって", "震えながら", "怯えながら", "不安定",
        "crazy", "crazier", "craziest", "mad", "madness", "insane", "insanity", "dizzy", "dizziness", "bewildering", "disoriented", "disorientation",
        "unclear", "indecisive", "indecision", "adrift", "aimless", "aimlessly", "confuse", "confuses", "confusing", "messy", "tangle", "tangled",
        "tangling", "entangled", "fall", "falls", "fell", "falling", "lost myself", "lost yourself", "losing myself", "lose myself", "lose control", "losing control",
        "lost control", "不明白", "搞不懂", "看不清", "找不到方向", "迷途", "徬徨", "彷徨", "徘徊", "混亂", "混乱", "凌亂",
        "凌乱", "瘋狂", "疯狂", "失控", "墜落", "坠落", "跌落", "落下", "迷ってる", "迷っている", "わからない", "分からない",
        "わからなくて", "分からなくて", "わからなかった", "分からなかった", "惑わされ", "狂って", "狂った", "狂う", "狂いそう", "落ちて", "落ちた", "落ちる",
        "堕ちて", "堕ちた", "堕ちる", "gentle", "gently", "gentleness", "softly", "warmth", "warmhearted", "warmheartedness", "safe", "safely",
        "safety", "shelter", "sheltered", "sheltering", "rest", "resting", "rested", "restful", "relax", "relaxed", "relaxing", "acceptance",
        "accepted", "embrace peace", "let me go", "let you go", "letting you go", "letting me go", "釋懷", "释怀", "豁然", "坦然", "安然", "平和",
        "平安", "溫暖", "温暖", "暖意", "慰藉", "撫慰", "抚慰", "庇護", "庇护", "避風港", "避风港", "溫柔以待",
        "温柔以待", "優しい", "優しく", "優しかった", "やさしい", "やさしく", "温もり", "ぬくもり", "暖かい", "暖かく", "あたたかい", "癒され",
        "癒して", "癒える", "安らいで", "微睡み", "まどろみ", "red", "crimson", "scarlet", "black heart", "blackhearted", "raging", "enraged",
        "outraged", "outrage", "resent", "resented", "resentment", "resentful", "revenge", "vengeance", "vengeful", "hostile", "hostility", "hateful",
        "spite", "spiteful", "malice", "malicious", "manipulate", "manipulated", "manipulating", "manipulation", "manipulative", "abuse", "abused", "abusive",
        "kill", "kills", "killing", "killer", "killers", "murder", "murdered", "murderous", "ruthless", "merciless", "憎惡", "憎恶",
        "怨恨", "怨念", "怨懟", "怨怼", "報復", "报复", "復仇", "复仇", "惡意", "恶意", "殘忍", "残忍",
        "殘酷", "残酷", "操控", "虐待", "殺戮", "杀戮", "殺意", "杀意", "殺す", "殺して", "殺した", "殺され",
        "憎い", "憎んで", "憎む", "恨み", "恨んで", "恨む", "復讐", "怒って", "怒った", "怒る", "許さない", "black",
        "pitch black", "pitchblack", "blackened", "blackness", "gloom", "gloomy", "dim", "dimmed", "dusky", "暗黒", "漆黑一片", "陰暗",
        "阴暗", "陰影", "阴影", "暗淡", "黯淡", "暗い", "暗く", "暗かった", "真っ暗", "暗闇の中", "white", "pure white",
        "delight", "delighted", "delightful", "ecstatic", "ecstasy", "elated", "elation", "bliss", "blissful", "blissfully", "cheer", "cheers",
        "cheerful", "cheerfully", "laugh", "laughs", "laughed", "laughing", "laughter", "smile", "smiles", "smiled", "smiling", "grin",
        "grinning", "grateful", "gratitude", "thankful", "慶幸", "庆幸", "感激", "感謝", "感谢", "欣喜", "喜悅", "喜悦",
        "歡喜", "欢喜", "喜歡", "喜欢", "歡笑", "欢笑", "微笑", "笑容", "開心", "开心", "雀躍", "雀跃",
        "笑って", "笑った", "笑える", "笑顔", "嬉しい", "嬉しく", "嬉しかった", "楽しい", "楽しく", "楽しかった", "喜び", "喜んで",
        "喜ぶ", "embarrassed", "embarrassment", "humiliated", "humiliation", "selfish", "selfishly", "selfishness", "unfaithful", "ashamedly", "後ろめたい", "申し訳ない",
        "恥ずかしく", "恥ずかしかった", "自私", "自慚", "自惭", "羞辱", "屈辱", "難堪", "难堪", "greed", "greedy", "greedily",
        "covet", "covets", "coveted", "coveting", "covetous", "desperate for you", "cant let go", "cannot let go", "want you only", "求而不得", "求之不得", "欲罷不能",
        "欲罢不能", "妒火", "妒意", "妒恨", "貪欲", "贪欲", "欲望", "渇望", "欲しい", "欲しくて", "欲しかった", "reds",
        "reddish", "ruby", "burgundy", "maroon", "紅色", "红色", "赤紅", "赤红", "朱紅", "朱红", "緋紅", "绯红",
        "赤い", "赤く", "赤色", "紅い", "紅く", "緋色", "真っ赤", "orange", "oranges", "orangish", "amber", "copper",
        "coral", "tangerine", "橙色", "橘色", "琥珀色", "銅色", "铜色", "珊瑚色", "オレンジ", "琥珀", "yellow", "yellows",
        "yellowish", "lemon yellow", "黃色", "黄色", "金黃色", "金黄色", "鵝黃", "鹅黄", "黄色い", "黄色く", "真っ黄色", "レモン色",
        "greens", "greenish", "jade", "lime green", "綠色", "绿色", "翠綠", "翠绿", "碧綠", "碧绿", "嫩綠", "嫩绿",
        "緑色", "緑の", "みどり", "翠色", "bluer", "bluest", "bluish", "sky blue", "baby blue", "light blue", "royal blue", "天藍",
        "天蓝", "湛藍", "湛蓝", "蔚藍", "蔚蓝", "碧藍", "碧蓝", "青い", "青く", "青色", "蒼い", "蒼く",
        "蒼色", "水色", "空色", "ブルー", "purple", "purples", "purplish", "violet", "violets", "amethyst", "紫色", "深紫",
        "紫紅", "紫红", "紫の", "紫に", "むらさき", "パープル", "菫色", "lavender", "lilac", "淡紫", "淺紫", "浅紫",
        "藤色", "薄紫", "ラベンダー", "pink", "pinks", "pinkish", "magenta", "fuchsia", "rose pink", "粉色", "粉紅", "粉红",
        "桃紅", "桃红", "洋紅", "洋红", "桃色", "桜色", "ピンク", "薔薇色", "cyan", "aqua", "aquamarine", "turquoise",
        "青綠", "青绿", "青藍", "青蓝", "碧色", "青緑", "シアン", "ターコイズ", "teal", "mint", "mint green", "薄荷綠",
        "薄荷绿", "薄荷色", "ミント色", "brown", "browns", "brownish", "umber", "sepia", "beige", "bronze", "棕色", "褐色",
        "茶色", "咖啡色", "赭色", "セピア", "ブラウン", "ベージュ", "黑色", "烏黑", "乌黑", "墨黑", "黑漆漆", "黒い",
        "黒く", "黒色", "黒の", "真っ黒", "漆黒", "ブラック", "whites", "whitish", "ivory", "白色", "雪白", "純白",
        "纯白", "乳白", "象牙白", "白い", "白く", "白の", "真っ白", "真白", "ホワイト", "grays", "greys", "grayish",
        "greyish", "灰暗", "灰燼色", "灰烬色", "グレー", "灰の色", "silver", "silvery", "platinum", "銀色", "银色", "銀白",
        "银白", "白金色", "銀の", "銀に", "シルバー", "golden yellow", "金色", "金黃", "金黄", "黄金色", "金の", "ゴールド"
        };
        expandedKeywordPalette = new int[] {
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 1, 1, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
        4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 6, 6,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 11, 11, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 11, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 11, 11, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 9,
        9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9,
        9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9,
        9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9,
        9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9,
        9, 9, 9, 9, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11,
        11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 4, 4, 4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11,
        11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 13, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9,
        9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9,
        9, 9, 9, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11,
        11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 18, 18, 18, 18, 18, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 17, 17, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 5, 5, 5, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 21, 6, 6, 6, 6, 6, 6, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 9, 9, 9, 9, 9, 9, 9, 9, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 21, 21, 21, 21,
        21, 21, 21, 21, 21, 21, 21, 21, 13, 13, 13, 13, 13, 13, 13, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 18, 18, 18, 18, 18, 18,
        18, 18, 18, 18, 18, 18, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 10, 10, 10, 10, 10, 10, 10, 10, 10, 19, 19, 19, 19, 19, 19,
        19, 19, 19, 19, 19, 8, 8, 8, 8, 8, 8, 8
        };
        expandedKeywordVersion = 7;
    }
    
    [HideInInspector] public int builtLineCount;
    [HideInInspector] public bool isActive;
    [HideInInspector] public bool estimatedTiming;
    [HideInInspector] public bool wasTruncated;

    private float[] starts = new float[0], ends = new float[0];
    private string[] texts = new string[0];
    private int[] wordStarts = new int[0], wordCounts = new int[0];
    private float[] wordTimes = new float[0], wordEnds = new float[0];
    private string[] words = new string[0];
    private Vector2[] positions;
    private float[] widths, sizes, heights;
    private Vector2[] characterPositions;
    private Vector2[] characterVisualBounds;
    [Range(0, 1)] public float cameraRhythmStrength = 0.32f;
    [Range(0, 1)] public float paragraphCameraStrength = 0.65f;
    private int[] cameraGroupFirst, cameraGroupLast, cameraGroupMode;
    private float[] phrasePresentation, characterKeywordStyle;
    private float[] characterTimes;
    private float[] characterRowStarts, characterNextRows;
    private int[] characterCounts, layoutRows;
    private int[] characterOffsets, slotLines;
    private int[] characterKeywordPalette;
    private bool[] keywordLayoutBuilt;
    private bool[] layoutBuilt;
    private int[] focusLines;
    private int[] sectionFirst, sectionIds, nextVisibleLines;
    private float[] flightStarts, flightDurations;
    private string[] phraseKeys;
    private float[] refrainWeights;
    private int refrainDetectionLevel;
    private int[] refrainNextLines;
    private Vector2[] refrainWindows;
    private float loadFade = 1f;
    private float screenLoadOpacity = 1f, screenEndOpacity = 1f;
    private int activeLine = -1;
    private float bridgeStart = -100f, bridgeDuration, bridgeLift;
    private Vector3 bridgeFrom;
    private float bridgeFromSize;
    [HideInInspector] public Vector3 cameraLocalTarget;
    [HideInInspector] public int focusedLayoutRow;
    private bool[] originalTextEnabled;
    private string lastUrl = "";
    private bool stopped;
    private bool initialized;
    private int buildCursor;
    private float nextSourceCheck;
    private float lastTime = -100f;
    private int lastLine = -1;
    private Vector3 cameraPosition;
    private Vector3 cameraVelocity;
    private float cameraSize = 4.5f;
    private int observedRevision = -1;
    [HideInInspector] public bool eligibleSource;
    [HideInInspector] public float heartbeat;
    private bool renderReady;
    private int renderWarmup;
    private bool exiting;
    private float bassVisibility;
    private float lastBassUpdate;
    private float highMidRotation;
    private Vector3 reactiveBands;
    private Texture2D coverTexture;
    private Color32[] coverPixels;
    private string coverHex = "", coverSong = "";
    private int coverCursor;
    private float coverBlend;
    private bool coverReady;
    private Vector3 accentSum;
    private float accentWeight;
    private Color coverAccent = Color.white;
    [HideInInspector] public float interludeAmount;
    private bool hasVisibleLyrics, enterFromAmbient;
    private float statusBrandExit = -1f;
    private string renderedStatus = "";
    private float statusClock = -100f, ambientClock;
    private Mesh statusGeometry;

    private void Start()
    {
        Initialize();
        if (!initialized) return;
        InitializeLtcgiOutput();
        core._AddListener(this, "_OnVideoBeginLoad");
        core._AddListener(this, "_onVideoEnd");
        core._AddListener(this, "_OnVideoError");
        CheckSource();
        if (diagnostics) Debug.Log("[Lyralume] Start: glyphs=" + glyphCodes.Length + ", screens=" + screens.Length + ", mode=" + displayMode);
    }

    public void Initialize()
    {
        if (initialized) return;
        if (exiting || stageCamera == null || output == null || glyphMaterial == null || articleRoot == null) return;
        initialized = true;
        slotLines = new int[lineMeshes.Length];
        for (int i = 0; i < slotLines.Length; i++) slotLines[i] = -1;
        originalTextEnabled = new bool[legacyTexts.Length];
        for (int i = 0; i < legacyTexts.Length; i++)
            originalTextEnabled[i] = legacyTexts[i] != null && legacyTexts[i].enabled;
        cameraPosition = stageCamera.transform.position;
        stageCamera.enabled = false;
        stageCamera.targetTexture = output;
        stageCamera.useOcclusionCulling = false;
        glyphMaterial.SetFloat("_StageY", articleRoot.position.y);
        glyphMaterial.SetFloat("_AccentBlend", 0);
        glyphMaterial.SetFloat("_InterludeAmount", 0);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_StageY", articleRoot.position.y);
        for (int i = 0; i < screenMaterials.Length; i++)
            if (screenMaterials[i] != null) screenMaterials[i].SetTexture("_FoliaTex", output);
        for (int i = 0; i < lineMeshes.Length; i++) lineMeshes[i].gameObject.SetActive(false);
        if (statusMesh != null) statusMesh.gameObject.SetActive(false);
        if (translationMesh != null) translationMesh.gameObject.SetActive(false);
        if (romanizationMesh != null) romanizationMesh.gameObject.SetActive(false);
        if (secondaryMaterial != null) { secondaryMaterial.SetFloat("_StageY", articleRoot.position.y); secondaryMaterial.SetFloat("_SongTime", 1); }
        UpdateBackgroundFrame();
    }

    // Install after VizVid's build preprocess has finished; keep its stock material as the video input.
    public void InitializeLtcgiOutput()
    {
        if (ltcgiOutput == null || ltcgiCompositeMaterial == null || ltcgiInitialized) return;
        ltcgiVideoMaterial = ltcgiOutput.material;
        if (ltcgiVideoMaterial == null || ltcgiVideoMaterial == ltcgiCompositeMaterial) return;
        ltcgiInitialized = true;
        ltcgiOutput.material = ltcgiCompositeMaterial;
        RefreshLtcgiOutput();
    }

    public void RefreshLtcgiOutput()
    {
        if (!ltcgiInitialized || ltcgiCompositeMaterial == null || ltcgiVideoMaterial == null || ltcgiOutput == null) return;
        if (isActiveAndEnabled && !exiting && ltcgiOutput.material == ltcgiVideoMaterial)
            ltcgiOutput.material = ltcgiCompositeMaterial;
        // VizVid continues writing to its original material, including AVPro/stereo/brightness conversion.
        ltcgiCompositeMaterial.SetTexture("_MainTex", ltcgiVideoMaterial.GetTexture("_MainTex"));
        ltcgiCompositeMaterial.SetColor("_Color", ltcgiVideoMaterial.GetColor("_Color"));
        ltcgiCompositeMaterial.SetFloat("_IsAVProVideo", ltcgiVideoMaterial.GetFloat("_IsAVProVideo"));
        ltcgiCompositeMaterial.SetFloat("_ScaleMode", ltcgiVideoMaterial.GetFloat("_ScaleMode"));
        ltcgiCompositeMaterial.SetFloat("_AspectRatio", ltcgiVideoMaterial.GetFloat("_AspectRatio"));
        ltcgiCompositeMaterial.SetFloat("_EmissionIntensity", ltcgiVideoMaterial.GetFloat("_EmissionIntensity"));
        ltcgiCompositeMaterial.SetVector("_StereoShift", ltcgiVideoMaterial.GetVector("_StereoShift"));
        ltcgiCompositeMaterial.SetVector("_StereoExtend", ltcgiVideoMaterial.GetVector("_StereoExtend"));
        ltcgiCompositeMaterial.SetTexture("_FoliaTex", output);
        ltcgiCompositeMaterial.SetFloat("_UseFolia", isActive && isActiveAndEnabled && !exiting ? 1 : 0);
        if (screenMaterials.Length > 0 && screenMaterials[0] != null)
        {
            ltcgiCompositeMaterial.SetFloat("_FoliaOpacity", screenMaterials[0].GetFloat("_ScreenOpacity"));
            ltcgiCompositeMaterial.SetColor("_FoliaTint", screenMaterials[0].GetColor("_Color"));
            ltcgiCompositeMaterial.SetFloat("_FoliaBrightness", screenMaterials[0].GetFloat("_EmissionIntensity"));
        }
    }

    public void RestoreLtcgiOutput()
    {
        if (ltcgiOutput != null && ltcgiCompositeMaterial != null && ltcgiOutput.material == ltcgiCompositeMaterial)
            ltcgiOutput.material = ltcgiVideoMaterial;
    }

    // Only direct NetEase songs and the world's observed BiliPlayer wrapper qualify.
    public bool IsNetEaseUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        string lower = url.ToLower();
        lower = lower.Replace("%3a", ":").Replace("%2f", "/").Replace("%3f", "?")
            .Replace("%3d", "=").Replace("%26", "&").Replace("%23", "#");
        string source = lower;
        if (lower.StartsWith("https://biliplayer.91vrchat.com/player/"))
        {
            int marker = lower.IndexOf("?url=");
            if (marker < 0) marker = lower.IndexOf("&url=");
            if (marker < 0) return false;
            source = lower.Substring(marker + 5);
        }
        if (!source.StartsWith("https://music.163.com/") && !source.StartsWith("http://music.163.com/")) return false;
        bool songRoute = source.StartsWith("https://music.163.com/song?") || source.StartsWith("http://music.163.com/song?") ||
            source.StartsWith("https://music.163.com/#/song?") || source.StartsWith("http://music.163.com/#/song?");
        if (!songRoute) return false;
        int at = source.IndexOf("?id=");
        if (at < 0) at = source.IndexOf("&id=");
        if (at < 0 || at + 4 >= source.Length) return false;
        int start = at + 4, end = start;
        while (end < source.Length && source[end] >= '0' && source[end] <= '9') end++;
        return end > start && end - start <= 12 && source[start] != '0' &&
            (end == source.Length || source[end] == '&' || source[end] == '#');
    }

    private void CheckSource()
    {
        if (exiting || core == null) return;
        string url = core.Url == null ? "" : core.Url.Get();
        if (url != lastUrl) { lastUrl = url; stopped = false; ResetCover(); ambientClock = 0; renderedStatus = ""; loadFade = 0; screenLoadOpacity = 0; screenEndOpacity = 1; ApplyScreenFade(); }
        eligibleSource = visualEnabled && lyrics != null && lyrics.systemEnabled && !stopped && (!restrictToNetEase || IsNetEaseUrl(url)) && (core.IsLoading || core.IsReady || core.IsPlaying || core.IsPaused);
        ApplyVisibility();
    }

    public void _OnVideoBeginLoad() { stopped = false; CheckSource(); loadFade = 0; screenLoadOpacity = 0; screenEndOpacity = 1; ApplyScreenFade(); }
    public void _onVideoEnd() { stopped = true; eligibleSource = false; renderReady = false; ApplyVisibility(); }
    public void _OnVideoError() { CheckSource(); }

    // Swap only the two configured renderer materials. VizVid keeps updating its existing MPB.
    public void SetActive(bool value)
    {
        Initialize();
        if (!initialized) return;
        if (!value) HideSecondaryLabels();
        if (isActive == value) return;
        isActive = value;
        RefreshLtcgiOutput();
        if (stageCamera != null) stageCamera.enabled = value;
        if (screenOutput != null) screenOutput.SetVisible(value);
        for (int i = 0; i < legacyTexts.Length; i++)
            if (legacyTexts[i] != null) legacyTexts[i].enabled = value ? false : originalTextEnabled[i];
        lastTime = -100f;
        if (diagnostics && !exiting) Debug.Log("[Lyralume] Screen takeover=" + value + ", built=" + builtLineCount + ", RT=" + (output != null && output.IsCreated()));
    }

    private void ApplyVisibility()
    {
        if (exiting || stageCamera == null) return;
        bool requested = displayMode == 2 && eligibleSource;
        bool takeover = requested && renderReady;
        SetActive(takeover);
        // Render first while retaining the original screen, then switch after two frames.
        stageCamera.enabled = requested;
        bool legacy = displayMode == 1;
        for (int i = 0; i < legacyTexts.Length; i++)
            if (legacyTexts[i] != null) legacyTexts[i].enabled = legacy && originalTextEnabled[i];
    }

    private void OnDisable() { if (initialized) SetActive(false); RestoreLtcgiOutput(); bassVisibility = 0; if (backgroundMaterial != null) backgroundMaterial.SetFloat("_BassEnabled", 0); }
    private void OnApplicationQuit() { exiting = true; RestoreLtcgiOutput(); if (stageCamera != null) stageCamera.enabled = false; }

    // Data comes from the existing downloader/parser, including YRC end times.
    public void LoadTimeline(float[] lineStarts, float[] lineEnds, string[] lineTexts,
        int[] firstWords, int[] counts, float[] times, float[] endTimes, string[] wordTexts, bool precise)
    {
        Initialize();
        if (!initialized || exiting) return;
        starts = lineStarts; ends = lineEnds; texts = lineTexts;
        overviewRevision++;
        enterFromAmbient = isActive && !hasVisibleLyrics;
        wordStarts = firstWords; wordCounts = counts; wordTimes = times; wordEnds = endTimes; words = wordTexts;
        estimatedTiming = !precise;
        glyphMaterial.SetFloat("_EndingProgress", 0);
        glyphMaterial.SetFloat("_FinalLineHold", 0);
        interludeAmount = 0;
        glyphMaterial.SetFloat("_InterludeAmount", 0);
        if (backgroundMaterial != null) { backgroundMaterial.SetFloat("_InterludeAmount", 0); backgroundMaterial.SetFloat("_EndingProgress", 0); }
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_LyricFocusActive", 0);
        buildCursor = 0; builtLineCount = 0; lastLine = -1; lastTime = -100f;
        activeLine = -1; bridgeStart = -100f;
        cameraVelocity = Vector3.zero;
        positions = new Vector2[texts.Length];
        widths = new float[texts.Length]; sizes = new float[texts.Length]; heights = new float[texts.Length];
        characterCounts = new int[texts.Length]; layoutRows = new int[texts.Length];
        characterOffsets = new int[texts.Length]; layoutBuilt = new bool[texts.Length];
        focusLines = new int[texts.Length];
        phraseKeys = new string[texts.Length]; refrainWeights = new float[texts.Length];
        int previousVisible = -1;
        for (int i = 0; i < texts.Length; i++)
        {
            phraseKeys[i] = NormalizeRefrainPhrase(texts[i]);
            if (!string.IsNullOrEmpty(texts[i]) && texts[i].Trim().Length > 0) previousVisible = i;
            focusLines[i] = previousVisible;
        }
        // Leading silence frames the first paragraph; internal silence retains the previous paragraph.
        int nextVisible = -1;
        for (int i = texts.Length - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(texts[i]) && texts[i].Trim().Length > 0) nextVisible = i;
            if (focusLines[i] < 0) focusLines[i] = nextVisible;
        }
        hasVisibleLyrics = nextVisible >= 0;
        statusBrandExit = -1f;
        PlanPhrasePresentation();
        PlanRefrains();
        if (!hasVisibleLyrics) enterFromAmbient = false;
        PlanSections();
        PlanCameraParagraphs();
        loadFade = 0;
        glyphMaterial.SetFloat("_LoadOpacity", 1);
        glyphMaterial.SetFloat("_EndOpacity", 1);
        screenLoadOpacity = 1; screenEndOpacity = 1; ApplyScreenFade();
        if (secondaryMaterial != null) { secondaryMaterial.SetFloat("_LoadOpacity", 1); secondaryMaterial.SetFloat("_EndOpacity", 1); }
        int totalCharacters = 0;
        for (int i = 0; i < texts.Length; i++)
        {
            characterOffsets[i] = totalCharacters;
            totalCharacters += texts[i] == null ? 0 : texts[i].Length;
        }
        characterVisualBounds = new Vector2[totalCharacters];
        characterPositions = new Vector2[totalCharacters]; characterTimes = new float[totalCharacters];
        characterKeywordStyle = new float[totalCharacters];
        characterKeywordPalette = new int[totalCharacters]; keywordLayoutBuilt = new bool[texts.Length];
        characterRowStarts = new float[totalCharacters]; characterNextRows = new float[totalCharacters];
        // A timeline change must not interrupt an already-rendering Folia background/status screen.
        renderReady = renderReady && output != null && output.IsCreated();
        if (!renderReady) renderWarmup = 0;
        wasTruncated = false;
        for (int i = 0; i < lineMeshes.Length; i++) { lineMeshes[i].gameObject.SetActive(false); slotLines[i] = -1; }
        if (statusText != null) statusText.text = texts.Length == 0 ? "等待歌詞" : "歌詞排版中…";
        ApplyVisibility();
        if (diagnostics) Debug.Log("[Lyralume] Timeline lines=" + texts.Length + ", precise=" + precise);
    }

    public void SetStatus(string text)
    {
        if (lyrics != null) lyrics.lyricStatus = string.IsNullOrEmpty(text) ? "正在載入歌詞" : text;
    }

    private void Update()
    {
        if (exiting || core == null || lyrics == null || stageCamera == null || output == null) return;
        if (!visualEnabled || !lyrics.systemEnabled || (screenOutput != null && !screenOutput.outputEnabled)) { SetActive(false); stageCamera.enabled = false; return; }
        heartbeat = Time.realtimeSinceStartup;
        UpdateBackgroundAudio();
        BuildCoverStep();
        int selection = modeSwitch == null ? displayMode : (int)modeSwitch.GetProgramVariable("ActiveIndex");
        if (selection >= 0 && selection <= 2 && selection != displayMode)
        {
            displayMode = selection;
            renderReady = false; renderWarmup = 0;
            ApplyVisibility();
            if (diagnostics) Debug.Log("[Lyralume] Mode=" + displayMode);
        }
        if (lyrics.timelineRevision != observedRevision)
        {
            observedRevision = lyrics.timelineRevision;
            LoadTimeline(lyrics.lineTimes, lyrics.lineEnds, lyrics.lineTexts, lyrics.lineWordStarts,
                lyrics.lineWordCounts, lyrics.wordTimes, lyrics.wordEnds, lyrics.wordTexts, lyrics.isYrc);
        }
        if (Time.realtimeSinceStartup >= nextSourceCheck)
        {
            nextSourceCheck = Time.realtimeSinceStartup + 0.1f;
            CheckSource();
        }
        if (displayMode != 2 || !eligibleSource) { RefreshLtcgiOutput(); return; }
        float time = core.Time + lyrics.lyricOffsetSeconds;
        if (core.IsLoading && !hasVisibleLyrics)
        {
            ambientClock += Mathf.Clamp(Time.unscaledDeltaTime, 0, 0.1f);
            time = ambientClock;
        }
        else ambientClock = Mathf.Max(0, time);
        RenderFrameAtTime(time);
        if (!isActive) loadFade = 0;
        AdvanceLoadFade(Time.unscaledDeltaTime);
        RefreshLtcgiOutput();
    }

    // Background/status rendering is available before a single lyric mesh exists.
    public void RenderFrameAtTime(float time)
    {
        Initialize();
        if (!initialized || exiting) return;
        PrepareAtTime(time);
        if (IsLineReady(activeLine))
        {
            if (statusMesh != null && statusMesh.gameObject.activeSelf)
            {
                if (statusBrandExit < 0 || time < statusBrandExit) statusBrandExit = time;
                float brandOpacity = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - statusBrandExit) / 0.2f));
                glyphMaterial.SetFloat("_StatusBrandOpacity", brandOpacity);
                if (brandOpacity <= 0.001f) statusMesh.gameObject.SetActive(false);
            }
            RenderAtTime(time);
        }
        else
        {
            string message = texts.Length > 0 && !hasVisibleLyrics ? "暫無歌詞" : lyrics == null ? "正在載入歌詞" : lyrics.lyricStatus;
            if (string.IsNullOrEmpty(message)) message = "正在載入歌詞";
            RenderStatusMessage(message, time);
            statusBrandExit = -1f;
            glyphMaterial.SetFloat("_StatusBrandOpacity", Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - statusClock) / 0.22f)));
            RenderAmbientAtTime(time);
            ApplySongEnding(time, core == null ? 0 : core.Duration, lyrics == null ? 0 : lyrics.lyricOffsetSeconds, core != null && (core.Loop || core.IsRangeLooping));
        }
        UpdateSecondaryLabels(time);
        if (!renderReady)
        {
            stageCamera.enabled = true;
            renderWarmup++;
            if (renderWarmup >= 3 && output != null && output.IsCreated()) { renderReady = true; ApplyVisibility(); }
        }
    }

    public void BuildNextLine()
    {
        if (buildCursor >= texts.Length || lineMeshes.Length == 0) return;
        int index = buildCursor++;
        EnsureLayout(index);
        BuildLine(index, true);
    }

    // Lifecycle fade uses elapsed wall time; timeline preview/seek rendering stays deterministic.
    public void AdvanceLoadFade(float delta)
    {
        loadFade = Mathf.Min(1, loadFade + Mathf.Clamp(delta, 0, 0.1f) / 0.22f);
        float opacity = Mathf.SmoothStep(0, 1, loadFade);
        screenLoadOpacity = opacity;
        ApplyScreenFade();
    }

    private void ApplyScreenFade()
    {
        for (int i = 0; i < screenMaterials.Length; i++)
            if (screenMaterials[i] != null) screenMaterials[i].SetFloat("_ScreenOpacity", screenLoadOpacity * screenEndOpacity);
    }

    public int FindLineAtTime(float time)
    {
        if (starts.Length == 0) return -1;
        int low = 0, high = starts.Length - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (starts[middle] <= time) low = middle; else high = middle - 1;
        }
        return low;
    }

    public bool IsLineReady(int index)
    {
        return index >= 0 && index < texts.Length && slotLines.Length > 0 && slotLines[index % slotLines.Length] == index;
    }

    public int FindFocusLineAtTime(float time)
    {
        int line = FindLineAtTime(time);
        return line < 0 ? -1 : focusLines[line];
    }

    // Search the complete +/-10-line window around the ten-line target before fixing a boundary.
    private void PlanSections()
    {
        int count = texts.Length;
        sectionFirst = new int[count]; sectionIds = new int[count]; nextVisibleLines = new int[count];
        flightStarts = new float[count]; flightDurations = new float[count];
        int[] visibleLines = new int[count]; float[] units = new float[count];
        bool[] breaks = new bool[count];
        int visibleCount = 0, next = -1, previous = -1, blank = -1;
        for (int i = count - 1; i >= 0; i--)
        {
            nextVisibleLines[i] = next;
            if (focusLines[i] == i) next = i;
        }
        for (int i = 0; i < count; i++)
        {
            flightStarts[i] = -100f;
            if (focusLines[i] != i) { if (blank < 0) blank = i; continue; }
            visibleLines[visibleCount++] = i;
            float rowWidth = 0, rowUnits = 1;
            float limit = Mathf.Max(1, Mathf.Min(columnWidth - 1.5f, estimatedTiming ? 10.4f : 15.2f));
            string text = texts[i];
            for (int c = 0; c < text.Length; c++)
            {
                if (text[c] == '\r') continue;
                if (text[c] == '\n') { rowUnits += Mathf.Max(1, Mathf.Ceil(rowWidth / limit)); rowWidth = 0; continue; }
                int glyph = FindGlyph(text[c]);
                if (glyph >= 0) rowWidth += glyphAdvances[glyph] * normalFontSize;
            }
            units[i] = rowUnits + Mathf.Max(0, Mathf.Ceil(rowWidth / limit) - 1);
            float windowStart = -100f, windowEnd = starts[i] - 0.08f;
            if (previous >= 0 && blank >= 0)
            {
                windowStart = Mathf.Max(starts[blank] + 0.06f, ends[previous] + 0.04f);
                windowStart = Mathf.Max(windowStart, windowEnd - 1.05f);
            }
            else if (previous >= 0 && !estimatedTiming && wordCounts[previous] > 0)
            {
                int word = wordStarts[previous] + wordCounts[previous] - 1;
                if (word >= 0 && word < words.Length && word < wordTimes.Length && word < wordEnds.Length)
                {
                    int letters = Mathf.Max(1, words[word] == null ? 1 : words[word].Trim().Length);
                    float perCharacter = (wordEnds[word] - wordTimes[word]) / letters;
                    if (perCharacter >= 1.35f)
                    {
                        windowEnd = Mathf.Min(windowEnd, wordEnds[word] - 0.12f);
                        windowStart = Mathf.Max(wordEnds[word] - perCharacter + 0.35f, windowEnd - 0.95f);
                    }
                }
            }
            if (windowStart > -99f && windowEnd - windowStart >= 0.4f)
            { flightStarts[i] = windowStart; flightDurations[i] = windowEnd - windowStart; }
            previous = i; blank = -1;
        }
        int blockStart = 0;
        while (blockStart < visibleCount)
        {
            int hardEnd = blockStart;
            float occupied = 0;
            // Twenty normal rows fit the requested grace; very large wrapped lyrics still stand alone.
            while (hardEnd < visibleCount && hardEnd - blockStart < 20)
            {
                float cost = units[visibleLines[hardEnd]];
                if (hardEnd > blockStart && occupied + cost > 24f) break;
                occupied += cost; hardEnd++;
            }
            int chosen = -1; float bestDistance = 100f, candidateUnits = 0;
            for (int boundary = blockStart + 1; boundary <= hardEnd && boundary < visibleCount; boundary++)
            {
                candidateUnits += units[visibleLines[boundary - 1]];
                int size = boundary - blockStart;
                if (size < 4 || candidateUnits < 5 || flightDurations[visibleLines[boundary]] <= 0) continue;
                float distance = Mathf.Abs(size - 10);
                if (distance < bestDistance) { bestDistance = distance; chosen = boundary; }
            }
            if (chosen < 0)
            {
                if (hardEnd == visibleCount && visibleCount - blockStart <= 10) break;
                chosen = Mathf.Min(blockStart + 10, hardEnd);
            }
            if (chosen >= visibleCount) break;
            breaks[visibleLines[chosen]] = true;
            blockStart = chosen;
        }
        int first = 0, section = 0;
        for (int i = 0; i < count; i++)
        {
            if (breaks[i]) { first = i; section++; }
            else { flightStarts[i] = -100f; flightDurations[i] = 0; }
            sectionFirst[i] = first; sectionIds[i] = section;
        }
    }

    public int GetSectionId(int line) { return sectionIds[line]; }

    // A visual block can contain several musical phrases separated by breathing space.
    private void PlanCameraParagraphs()
    {
        cameraGroupFirst = new int[texts.Length]; cameraGroupLast = new int[texts.Length]; cameraGroupMode = new int[texts.Length];
        int first = -1, previous = -1;
        for (int i = 0; i < texts.Length; i++)
        {
            cameraGroupFirst[i] = -1; cameraGroupLast[i] = -1;
            if (focusLines[i] != i) continue;
            bool silence = previous >= 0 && (starts[i] - ends[previous] >= 1.8f || (i > previous + 1 && starts[i] - starts[previous + 1] >= 0.8f));
            if (previous >= 0 && (sectionIds[i] != sectionIds[previous] || silence)) { FillCameraParagraph(first, previous); first = i; }
            if (first < 0) first = i;
            previous = i;
        }
        if (previous >= 0) FillCameraParagraph(first, previous);
    }
    private void FillCameraParagraph(int first, int last)
    {
        int letters = 0, lines = 0; float sungSeconds = 0;
        for (int i = first; i <= last; i++)
        {
            if (focusLines[i] != i) continue;
            lines++; for (int c = 0; c < texts[i].Length; c++) if (!char.IsWhiteSpace(texts[i][c])) letters++;
            sungSeconds += Mathf.Max(0.25f, ends[i] - starts[i]);
        }
        int mode = letters / Mathf.Max(sungSeconds, 0.25f) >= 9.5f ? 1 : lines >= 3 && letters <= lines * 7 ? 2 : 0;
        for (int i = first; i <= last; i++) { cameraGroupFirst[i] = first; cameraGroupLast[i] = last; cameraGroupMode[i] = mode; }
    }
    public int GetCameraParagraphFirst(int line) { return cameraGroupFirst[line]; }
    public int GetCameraParagraphLast(int line) { return cameraGroupLast[line]; }
    public int GetCameraParagraphMode(int line) { return cameraGroupMode[line]; }
    public float GetCameraParagraphEnvelope(int line, float time)
    {
        int first = cameraGroupFirst[line], last = cameraGroupLast[line];
        if (first < 0 || first == last) return 0;
        float finish = Mathf.Max(starts[last] + 0.1f, ends[last]);
        return Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - starts[first]) / 0.8f)) * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - finish + 0.9f) / 0.9f)));
    }
    public Vector3 GetParagraphCameraTarget(int line, float time, Vector3 reading)
    {
        int first = cameraGroupFirst[line], last = cameraGroupLast[line];
        if (first < 0 || first == last || paragraphCameraStrength <= 0) return reading;
        EnsureLayout(last);
        float phase = Mathf.Clamp01((time - starts[first]) / Mathf.Max(ends[last] - starts[first], 0.1f));
        float eased = phase - Mathf.Sin(phase * Mathf.PI * 2f) / (Mathf.PI * 2f);
        Vector2 from = positions[first] + new Vector2(widths[first] * 0.5f, sizes[first] * 0.35f);
        Vector2 to = positions[last] + new Vector2(widths[last] * 0.5f, sizes[last] * 0.35f);
        Vector2 guide = Vector2.Lerp(from, to, eased);
        int mode = cameraGroupMode[line];
        float blend = Mathf.Clamp01(paragraphCameraStrength) * (mode == 0 ? 0.18f : 0.08f);
        float maxX = mode == 0 ? 0.5f : 0.22f, maxY = mode == 0 ? 0.28f : 0.12f;
        // Retain the reading target as the safety frame; a shared route is only a bounded influence.
        return reading + new Vector3(Mathf.Clamp((guide.x - reading.x) * blend, -maxX, maxX), Mathf.Clamp((guide.y - reading.y) * blend, -maxY, maxY), 0);
    }
    public int GetOverviewRevision() { return overviewRevision; }
    public int GetOverviewLineCount() { return texts.Length; }

    // Reuse the actual glyph builder while diverting its output away from the singing pool.
    public Mesh BuildNativeOverviewMesh(Mesh mesh, int first, int stop)
    {
        first = Mathf.Max(0, first); stop = Mathf.Min(stop, texts.Length);
        int count = 0;
        for (int i = first; i < stop; i++) count += texts[i] == null ? 0 : texts[i].Length;
        var vertices = new Vector3[count * 8]; var uv = new Vector2[count * 8];
        var timing = new Vector2[count * 8]; var lineData = new Vector2[count * 8];
        var pivots = new Vector2[count * 8]; var exits = new Vector2[count * 8]; var history = new Vector2[count * 8]; var styling = new Vector2[count * 8]; var sectionTiming = new Vector2[count * 8];
        var colors = new Color32[count * 8]; var triangles = new int[count * 12];
        int vertexOffset = 0, triangleOffset = 0;
        for (int line = first; line < stop; line++)
        {
            EnsureLayout(line);
            buildingNativeOverview = true; BuildLine(line, true); buildingNativeOverview = false;
            Vector3[] v = nativeOverviewScratch.vertices; Vector2[] u = nativeOverviewScratch.uv;
            Vector2[] t = nativeOverviewScratch.uv2, d = nativeOverviewScratch.uv3;
            Vector2[] p = nativeOverviewScratch.uv4, e = nativeOverviewScratch.uv5;
            Vector2[] h = nativeOverviewScratch.uv6;
            Vector2[] style = nativeOverviewScratch.uv7;
            Vector2[] section = nativeOverviewScratch.uv8;
            Color32[] c = nativeOverviewScratch.colors32; int[] indices = nativeOverviewScratch.triangles;
            for (int j = 0; j < v.Length; j++)
            {
                int at = vertexOffset + j;
                vertices[at] = v[j]; uv[at] = u[j]; timing[at] = t[j]; lineData[at] = d[j];
                pivots[at] = p[j]; exits[at] = e[j]; history[at] = h[j]; colors[at] = c[j];
                styling[at] = style[j];
                sectionTiming[at] = section[j];
            }
            for (int j = 0; j < indices.Length; j++) triangles[triangleOffset + j] = indices[j] + vertexOffset;
            vertexOffset += v.Length; triangleOffset += indices.Length;
        }
        if (mesh == null) mesh = new Mesh(); else mesh.Clear();
        mesh.indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = timing; mesh.uv3 = lineData; mesh.uv4 = pivots; mesh.uv5 = exits;
        mesh.uv6 = history;
        mesh.uv7 = styling;
        mesh.uv8 = sectionTiming;
        mesh.colors32 = colors; mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
    }

    // Optional debug view: static one-quad glyphs, separate from the singing mesh pool and clocks.
    public Mesh BuildOverviewMesh(Mesh mesh, int first, int stop)
    {
        stop = Mathf.Min(stop, texts.Length); first = Mathf.Max(0, first);
        int count = 0;
        for (int line = first; line < stop; line++)
            if (!string.IsNullOrEmpty(texts[line])) for (int c = 0; c < texts[line].Length; c++)
                if (texts[line][c] != '\r' && texts[line][c] != '\n' && texts[line][c] != ' ' && texts[line][c] != '\t') count++;
        var vertices = new Vector3[count * 4]; var uv = new Vector2[count * 4]; var triangles = new int[count * 6];
        int used = 0;
        for (int line = first; line < stop; line++)
        {
            if (string.IsNullOrEmpty(texts[line]) || texts[line].Trim().Length == 0) continue;
            EnsureLayout(line);
            float size = sizes[line];
            for (int c = 0; c < texts[line].Length; c++)
            {
                char ch = texts[line][c];
                if (ch == '\r' || ch == '\n' || ch == ' ' || ch == '\t') continue;
                int glyph = FindGlyph(ch); if (glyph < 0) glyph = FindGlyph(63); if (glyph < 0) continue;
                Vector2 at = positions[line] + characterPositions[characterOffsets[line] + c];
                Vector4 metric = glyphMetrics[glyph], atlas = glyphUVs[glyph];
                float x = at.x + metric.x * size, y = at.y + metric.y * size;
                int v = used * 4, t = used * 6;
                vertices[v] = new Vector3(x, y, 0); vertices[v + 1] = new Vector3(x, y + metric.w * size, 0);
                vertices[v + 2] = new Vector3(x + metric.z * size, y + metric.w * size, 0); vertices[v + 3] = new Vector3(x + metric.z * size, y, 0);
                uv[v] = new Vector2(atlas.x, atlas.y); uv[v + 1] = new Vector2(atlas.x, atlas.w);
                uv[v + 2] = new Vector2(atlas.z, atlas.w); uv[v + 3] = new Vector2(atlas.z, atlas.y);
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3; used++;
            }
        }
        if (mesh == null) mesh = new Mesh(); else mesh.Clear();
        mesh.indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds();
        return mesh;
    }
    public float GetSectionFlightStart(int line) { return flightStarts[line]; }
    public float GetSectionFlightDuration(int line) { return flightDurations[line]; }

    public int FindCameraLineAtTime(float time)
    {
        int focus = FindFocusLineAtTime(time);
        if (focus < 0) return focus;
        int next = nextVisibleLines[focus];
        if (next >= 0 && flightDurations[next] > 0 && time >= flightStarts[next] && time < starts[next]) return next;
        return focus;
    }

    // Only nearby meshes stay resident. Full-song timing/layout metadata is independent of the mesh pool.
    public void PrepareAtTime(float time)
    {
        int previousActiveLine = activeLine;
        activeLine = FindFocusLineAtTime(time);
        if (activeLine < 0 || lineMeshes.Length == 0) return;
        if (!IsLineReady(activeLine)) { EnsureLayout(activeLine); BuildLine(activeLine, true); }
        int cameraLine = FindCameraLineAtTime(time);
        if (cameraLine != activeLine && !IsLineReady(cameraLine)) { EnsureLayout(cameraLine); BuildLine(cameraLine, true); }
        int behind = Mathf.Min(4, (lineMeshes.Length - 1) / 2);
        int ahead = Mathf.Min(7, lineMeshes.Length - behind - 1);
        // Avoid calling GameObject.SetActive across the whole pool every video frame.
        if (activeLine != previousActiveLine) for (int slot = 0; slot < slotLines.Length; slot++)
        {
            int line = slotLines[slot];
            bool visible = line >= Mathf.Max(0, activeLine - behind) && line <= activeLine + ahead;
            lineMeshes[slot].gameObject.SetActive(line >= 0 && visible);
        }
        // Prepare one neighbor per frame, nearest first; the current line never waits for earlier mesh builds.
        for (int distance = 1; distance <= Mathf.Max(behind, ahead); distance++)
        {
            int next = activeLine + distance;
            if (distance <= ahead && next < texts.Length && !IsLineReady(next)) { EnsureLayout(next); BuildLine(next, true); return; }
            int previous = activeLine - distance;
            if (distance <= behind && previous >= 0 && !IsLineReady(previous)) { EnsureLayout(previous); BuildLine(previous, true); return; }
        }
    }

    private void EnsureLayout(int index)
    {
        // Each column is independent, so jumping deep into a song measures at most ten preceding/current lines.
        int first = sectionFirst[index];
        for (int i = first; i <= index; i++) if (!layoutBuilt[i]) BuildLine(i, false);
    }

    public int[] MatchKeywordPalette(string text)
    {
        if (text == null) return new int[0];
        if (!keywordColors) return new int[text.Length];
        EnsureExpandedKeywordTable();
        if (text.Length == 0) return new int[0];
        int[] result = new int[text.Length]; int[] lengths = new int[text.Length];
        int customCount = keywordTerms == null || keywordPalette == null ? 0 : Mathf.Min(keywordTerms.Length, keywordPalette.Length);
        string lower = text.ToLower();
        for (int rule = 0; rule < customCount + expandedKeywordTerms.Length; rule++)
        {
            string entry = rule < customCount ? keywordTerms[rule] : expandedKeywordTerms[rule - customCount];
            int palette = rule < customCount ? keywordPalette[rule] : expandedKeywordPalette[rule - customCount];
            if (string.IsNullOrEmpty(entry) || palette < 1 || palette > 21) continue;
            string term = entry.ToLower(); bool latin = term[0] < 128;
            int search = 0;
            while (search <= lower.Length - term.Length)
            {
                int at = lower.IndexOf(term, search); if (at < 0) break;
                int stop = at + term.Length;
                bool boundary = !latin || ((at == 0 || !IsKeywordWordCharacter(lower[at - 1])) && (stop == lower.Length || !IsKeywordWordCharacter(lower[stop])));
                if (boundary)
                {
                    bool blocked = false;
                    for (int c = at; c < stop; c++) if (lengths[c] >= term.Length) blocked = true;
                    if (!blocked) for (int c = at; c < stop; c++) { result[c] = palette; lengths[c] = term.Length; }
                }
                search = at + 1;
            }
        }
        return result;
    }

    // Exact normalized repeats are presentation cues, never additional chorus detection.
    private void PlanPhrasePresentation()
    {
        phrasePresentation = new float[texts.Length];
        DataDictionary seen = new DataDictionary();
        for (int i = 0; i < texts.Length; i++)
        {
            string key = phraseKeys[i]; if (key.Length < 4) continue;
            DataToken previous; int count = seen.TryGetValue(key, out previous) ? previous.Int : 0;
            phrasePresentation[i] = Mathf.Min(count * 0.6f, 1f); seen.SetValue(key, count + 1);
        }
    }
    public float GetPhrasePresentation(int line) { return phrasePresentation != null && line >= 0 && line < phrasePresentation.Length ? phrasePresentation[line] : 0; }

    // Keep matching separate from visual ranking: dictionary coverage is unchanged.
    // Fractional palette bits: 0=strong, .125=gentle, .25=secondary. Integer part is the palette.
    public float[] RankKeywordPresentation(int[] palette)
    {
        float[] style = new float[palette.Length];
        int[] selected = new int[4]; float[] scores = new float[4];
        for (int c = 0; c < palette.Length; c++)
        {
            int id = palette[c]; if (id <= 0 || (c > 0 && palette[c - 1] == id)) continue;
            int stop = c + 1; while (stop < palette.Length && palette[stop] == id) stop++;
            // Repeated words share their palette's role; never dim only the third occurrence.
            bool alreadySelected = false;
            for (int slot = 0; slot < 4; slot++) if (selected[slot] == id) alreadySelected = true;
            if (alreadySelected) continue;
            float score = (id == 1 || id == 2 ? 4f : id == 3 || id == 5 || id >= 10 ? 3f : id == 4 ? 2f : 1f) + Mathf.Min(stop - c, 20) * 0.01f;
            for (int slot = 0; slot < 4; slot++)
            {
                if (score <= scores[slot]) continue;
                for (int shift = 3; shift > slot; shift--) { selected[shift] = selected[shift - 1]; scores[shift] = scores[shift - 1]; }
                selected[slot] = id; scores[slot] = score; break;
            }
        }
        for (int c = 0; c < palette.Length;)
        {
            int id = palette[c], stop = c + 1; while (stop < palette.Length && palette[stop] == id) stop++;
            bool primary = false;
            for (int slot = 0; slot < 4; slot++) if (selected[slot] == id) primary = true;
            float tier = primary ? (id == 6 || id == 7 || id == 8 || id == 9 ? 0.125f : 0f) : 0.25f;
            for (int j = c; j < stop; j++) style[j] = id > 0 ? id + tier : 0;
            c = stop;
        }
        return style;
    }

    private bool IsKeywordWordCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '\'' || value == '-' || value == '’';
    }

    public float GetRefrainStrength(int index)
    {
        return index >= 0 && index < texts.Length ? refrainWeights[index] : 0;
    }

    public string NormalizeRefrainPhrase(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string source = text.ToLower(); char[] result = new char[source.Length]; int length = 0; bool space = false;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (char.IsLetterOrDigit(c)) { if (space && length > 0) result[length++] = ' '; result[length++] = c; space = false; }
            else if (c != '\'' && c != '’') space = length > 0;
        }
        return new string(result, 0, length);
    }

    public Vector2 GetRefrainWindow(int index) { return refrainWindows != null && index >= 0 && index < refrainWindows.Length ? refrainWindows[index] : Vector2.zero; }

    private void PlanRefrains()
    {
        refrainDetectionLevel = 0;
        refrainSectionCount = 0;
        refrainNextLines = new int[texts.Length]; refrainWindows = new Vector2[texts.Length];
        int next = -1;
        for (int i = texts.Length - 1; i >= 0; i--) { refrainNextLines[i] = next; if (phraseKeys[i].Length > 0) next = i; refrainWindows[i] = new Vector2(starts[i], ends[i]); }
        int visible = 0;
        for (int i = 0; i < texts.Length; i++) if (phraseKeys[i].Length > 0) visible++;
        DataDictionary pairs = new DataDictionary(), counts = new DataDictionary();
        // Level 1: repeated distinct adjacent pairs.
        for (int first = 0; first + 1 < texts.Length; first++)
        {
            string key = RefrainPairKey(first); if (key.Length == 0) continue;
            DataToken previous;
            if (pairs.TryGetValue(key, out previous))
            {
                int other = previous.Int;
                if (refrainNextLines[other] >= first) continue;
                DataToken count; counts.TryGetValue(key, out count); counts.SetValue(key, count.Int + 1);
                refrainWeights[first] = 1; refrainWeights[refrainNextLines[first]] = 1;
                refrainWeights[other] = 1; refrainWeights[refrainNextLines[other]] = 1;
            }
            else { pairs.SetValue(key, first); counts.SetValue(key, 1); }
        }
        if (RefrainCoverageUsable(visible)) { refrainDetectionLevel = 1; BuildRefrainWindows(); return; }
        // Level 2: require the same substantial three-phrase sequence, filtering scattered pairs.
        ClearRefrains(); DataDictionary triples = new DataDictionary();
        for (int first = 0; first + 2 < texts.Length; first++)
        {
            string pair = RefrainPairKey(first);
            if (pair.Length == 0) continue;
            int second = refrainNextLines[first];
            if (RefrainPairKey(second).Length == 0) continue;
            int third = refrainNextLines[second];
            if (phraseKeys[first].Length + phraseKeys[second].Length + phraseKeys[third].Length < 24) continue;
            string key = pair.Length + ":" + pair + phraseKeys[third]; DataToken previous;
            if (triples.TryGetValue(key, out previous))
            {
                int other = previous.Int; if (refrainNextLines[refrainNextLines[other]] >= first) continue;
                refrainWeights[first] = 1; refrainWeights[second] = 1; refrainWeights[third] = 1;
                refrainWeights[other] = 1; refrainWeights[refrainNextLines[other]] = 1; refrainWeights[refrainNextLines[refrainNextLines[other]]] = 1;
            }
            else triples.SetValue(key, first);
        }
        if (RefrainCoverageUsable(visible)) { refrainDetectionLevel = 2; BuildRefrainWindows(); return; }
        // Level 3: keep only the most recurrent pair, requiring at least three occurrences.
        ClearRefrains(); int peak = 2;
        for (int first = 0; first + 1 < texts.Length; first++)
        {
            string key = RefrainPairKey(first); DataToken count;
            if (key.Length > 0 && counts.TryGetValue(key, out count)) peak = Mathf.Max(peak, count.Int);
        }
        if (peak >= 3) for (int first = 0; first + 1 < texts.Length; first++)
        {
            string key = RefrainPairKey(first); DataToken count;
            if (key.Length > 0 && counts.TryGetValue(key, out count) && count.Int == peak) { refrainWeights[first] = 1; refrainWeights[refrainNextLines[first]] = 1; }
        }
        if (RefrainCoverageUsable(visible)) { refrainDetectionLevel = 3; BuildRefrainWindows(); return; }
        ClearRefrains();
    }

    public int GetRefrainDetectionLevel() { return refrainDetectionLevel; }
    private string RefrainPairKey(int first)
    {
        if (first < 0 || first >= texts.Length || phraseKeys[first].Length < 4) return "";
        int second = refrainNextLines[first];
        if (second < 0 || phraseKeys[second].Length < 4 || phraseKeys[first] == phraseKeys[second] || phraseKeys[first].Length + phraseKeys[second].Length < 16 || starts[second] - ends[first] > 4f) return "";
        return phraseKeys[first].Length + ":" + phraseKeys[first] + phraseKeys[second];
    }
    private bool RefrainCoverageUsable(int visible)
    {
        int marked = 0; for (int i = 0; i < refrainWeights.Length; i++) if (refrainWeights[i] > 0) marked++;
        return marked > 0 && marked * 4 < visible * 3;
    }
    private void ClearRefrains() { for (int i = 0; i < refrainWeights.Length; i++) refrainWeights[i] = 0; }
    private void BuildRefrainWindows()
    {
        refrainSections = new Vector2[texts.Length]; refrainSectionCount = 0;
        for (int first = 0; first < texts.Length; first++)
        {
            if (refrainWeights[first] <= 0) continue;
            int last = first, next = refrainNextLines[last]; float lastEnd = ends[first];
            while (next >= 0 && refrainWeights[next] > 0 && starts[next] - ends[last] <= 2f) { last = next; lastEnd = Mathf.Max(lastEnd, ends[last]); next = refrainNextLines[last]; }
            Vector2 window = new Vector2(starts[first], Mathf.Max(starts[first] + 0.1f, lastEnd));
            refrainSections[refrainSectionCount++] = window;
            int line = first;
            while (line >= 0 && line <= last) { refrainWindows[line] = window; line = refrainNextLines[line]; }
            first = last;
        }
    }

    private Vector2[] refrainSections = new Vector2[0];
    private int refrainSectionCount;
    public float RefrainAmountAtTime(float time)
    {
        float amount = 0;
        for (int i = 0; i < refrainSectionCount; i++)
        {
            Vector2 window = refrainSections[i];
            float envelope = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - window.x) / .6f)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - window.y) / .9f)));
            amount = Mathf.Max(amount, envelope);
        }
        return amount;
    }

    private void BuildLine(int index, bool makeMesh)
    {
        string text = texts[index] == null ? "" : texts[index];
        int count = text.Length;
        float gap = index == 0 ? 3f : starts[index] - ends[index - 1];
        // Measure source rows; retain explicit breaks and bound font reduction.
        float readingWidth = Mathf.Min(columnWidth - 1.5f, estimatedTiming ? 10.4f : 15.2f);
        float sourceWidth = 0, widestSourceRow = 0;
        int sourceRows = 1;
        for (int c = 0; c < count; c++)
        {
            if (text[c] == '\n') { widestSourceRow = Mathf.Max(widestSourceRow, sourceWidth); sourceWidth = 0; sourceRows++; continue; }
            if (text[c] == '\r') continue;
            int measuredGlyph = FindGlyph(text[c]);
            if (measuredGlyph < 0) measuredGlyph = FindGlyph(63);
            if (measuredGlyph >= 0) sourceWidth += glyphAdvances[measuredGlyph] * normalFontSize;
        }
        widestSourceRow = Mathf.Max(widestSourceRow, sourceWidth);
        float normalWidth = widestSourceRow;
        float density = normalWidth / Mathf.Max(normalFontSize, 0.001f);
        float phraseDuration = Mathf.Max(0, ends[index] - starts[index]);
        bool hero = count >= 2 && density <= 14f && sourceRows <= 2 && (gap >= 2.5f || phraseDuration >= 1.4f);
        // Use measured width, not Latin/CJK character count, for stable hierarchy.
        float size = hero ? Mathf.Lerp(normalFontSize, Mathf.Max(normalFontSize, heroFontSize),
            Mathf.Lerp(1f, 0.05f, Mathf.InverseLerp(4, 14, density))) : normalFontSize;
        float refrain = GetRefrainStrength(index);
        float presentation = GetPhrasePresentation(index);
        if (normalWidth * size / Mathf.Max(normalFontSize, 0.001f) * 1.04f < readingWidth - 0.15f) size *= 1 + presentation * 0.04f;
        // A small repeat-section lift must never create a new wrap by itself.
        float liftedSize = Mathf.Min(Mathf.Max(normalFontSize, heroFontSize), size * (1 + refrain * 0.12f));
        if (normalWidth * liftedSize / Mathf.Max(normalFontSize, 0.001f) < readingWidth - 0.15f) size = liftedSize;
        widestSourceRow *= size / Mathf.Max(normalFontSize, 0.001f);
        // Do not turn a long emphasis line into multiple rows solely because of its hero size.
        if (hero && widestSourceRow * 0.78f > readingWidth)
        {
            widestSourceRow = normalWidth;
            size = normalFontSize;
        }
        if (widestSourceRow > readingWidth) size *= Mathf.Max(0.78f, (readingWidth - 0.03f) / widestSourceRow);

        int column = sectionIds[index];
        int row = index - sectionFirst[index];
        bool emphasis = size > normalFontSize * 1.15f;
        float indent = emphasis ? Mathf.Min(0.7f, Mathf.Max(0, readingWidth - normalWidth * size / Mathf.Max(normalFontSize, 0.001f)) * 0.1f) : 0;
        float paragraphGap = (emphasis || (row > 0 && sizes[index - 1] > normalFontSize * 1.15f) ? 0.66f : 0.5f) + refrain * 0.20f;
        Vector2 position = new Vector2((column - 1) * columnWidth + indent,
            row == 0 ? 0 : positions[index - 1].y - Mathf.Max(rowHeight * (emphasis ? 0.98f : 0.88f), heights[index - 1] + paragraphGap));
        positions[index] = position;
        sizes[index] = size;
        Vector3[] vertices = null;
        Vector2[] uv = null, timing = null, lineTiming = null;
        Color32[] colors = null;
        int[] triangles = null;
        if (makeMesh)
        {
            vertices = new Vector3[count * 8]; uv = new Vector2[count * 8];
            timing = new Vector2[count * 8]; lineTiming = new Vector2[count * 8];
            colors = new Color32[count * 8]; triangles = new int[count * 12];
        }
        float x = 0, y = 0, maxWidth = 0;
        int localWord = 0, wordOffset = 0;
        float lineEnd = ends[index] > starts[index] ? ends[index] :
            index + 1 < starts.Length ? starts[index + 1] : starts[index] + 5f;
        float entranceWindow = Mathf.Min(0.6f, Mathf.Max(0, lineEnd - starts[index]) * 0.35f);
        if (index + 1 < starts.Length) entranceWindow = Mathf.Min(entranceWindow, Mathf.Max(0, starts[index + 1] - starts[index]) * 0.35f);
        int focusBase = characterOffsets[index];
        characterCounts[index] = count;
        for (int c = 0; c < count; c++)
        {
            if (text[c] == '\r' || text[c] == '\n')
            {
                if (text[c] == '\n') { maxWidth = Mathf.Max(maxWidth, x); x = 0; y -= size * 1.5f; }
                characterPositions[focusBase + c] = new Vector2(x, y);
                characterTimes[focusBase + c] = c == 0 ? starts[index] : characterTimes[focusBase + c - 1];
                continue;
            }
            int code = text[c];
            int glyph = FindGlyph(code);
            if (glyph < 0) glyph = FindGlyph(63);
            if (glyph < 0) continue;
            float advance = glyphAdvances[glyph] * size;
            // Keep ordinary space-separated words together; long/CJK runs can still wrap by glyph.
            if (x > 0 && text[c] != ' ' && (c == 0 || text[c - 1] == ' '))
            {
                float nextWordWidth = 0;
                for (int next = c; next < count && text[next] != ' ' && text[next] != '\n' && text[next] != '\r'; next++)
                {
                    int nextGlyph = FindGlyph(text[next]);
                    if (nextGlyph >= 0) nextWordWidth += glyphAdvances[nextGlyph] * size;
                }
                if (x + nextWordWidth > readingWidth) { maxWidth = Mathf.Max(maxWidth, x); x = 0; y -= size * 1.5f; }
            }
            if (x + advance > readingWidth && x > 0) { maxWidth = Mathf.Max(maxWidth, x); x = 0; y -= size * 1.5f; }
            float start = starts[index], end = lineEnd;
            if (wordCounts[index] > 0)
            {
                int first = wordStarts[index];
                while (localWord < wordCounts[index] - 1 && c >= wordOffset + words[first + localWord].Length)
                { wordOffset += words[first + localWord].Length; localWord++; }
                int w = first + localWord;
                float duration = Mathf.Max(wordEnds[w] - wordTimes[w], 0.04f);
                int length = Mathf.Max(words[w].Length, 1);
                start = wordTimes[w] + duration * (c - wordOffset) / length;
                end = wordTimes[w] + duration * (c - wordOffset + 1) / length;
            }
            else
            {
                // LRC camera metadata retains its line clock. Mesh onset below is visual typing only.
                start = starts[index]; end = lineEnd;
            }
            characterPositions[focusBase + c] = new Vector2(x + advance * 0.5f, y);
            characterTimes[focusBase + c] = start;
            Vector4 metric = glyphMetrics[glyph], atlas = glyphUVs[glyph];
            characterVisualBounds[focusBase + c] = new Vector2(y + metric.y * size, y + (metric.y + metric.w) * size);
            if (!makeMesh) { x += advance; continue; }
            float left = position.x + x + metric.x * size;
            float bottom = position.y + y + metric.y * size;
            int v = c * 8;
            SetQuad(vertices, uv, colors, v, left, bottom, metric.z * size, metric.w * size,
                new Vector2(atlas.x, atlas.y), new Vector2(atlas.z, atlas.w), false);
            SetQuad(vertices, uv, colors, v + 4, position.x + x, position.y + y - size * 0.12f,
                Mathf.Max(advance, size * 0.12f), size * 1.06f, Vector2.zero, Vector2.one, true);
            for (int j = 0; j < 8; j++)
            {
                float visualStart = estimatedTiming ? starts[index] + entranceWindow * c / Mathf.Max(count - 1, 1) : start;
                timing[v + j] = new Vector2(visualStart, end);
                // 1 = aligned word/character, 2 = rapid LRC visual entrance, never singing alignment.
                lineTiming[v + j] = new Vector2(lineEnd, text[c] == ' ' || text[c] == '\t' ? 0f : estimatedTiming ? 2f : 1f);
                float rest = index + 1 < starts.Length ? Mathf.Max(0, starts[index + 1] - lineEnd) : 3f;
                byte hold = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(1.1f + rest * 0.3f, 1.1f, 3f) / 6f * 255f), 1, 255);
                colors[v + j] = new Color32(colors[v + j].r, hold, j % 4 >= 2 ? (byte)255 : (byte)0, text[c] == ' ' || text[c] == '\t' ? (byte)0 : (byte)255);
            }
            for (int q = 0; q < 2; q++)
            {
                int t = c * 12 + q * 6, b = v + q * 4;
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
                triangles[t + 3] = b; triangles[t + 4] = b + 2; triangles[t + 5] = b + 3;
            }
            x += advance;
        }
        widths[index] = Mathf.Max(maxWidth, x);
        float rowStartTime = starts[index], rowY = 0;
        for (int c = 0; c < count; c++)
        {
            if (text[c] == '\n' || text[c] == '\r') continue;
            if (characterPositions[focusBase + c].y < rowY - size * 0.3f) { rowY = characterPositions[focusBase + c].y; rowStartTime = characterTimes[focusBase + c]; }
            characterRowStarts[focusBase + c] = rowStartTime;
        }
        float nextRowTime = 10000000f; rowY = y;
        float firstTimeOnRow = lineEnd;
        for (int c = count - 1; c >= 0; c--)
        {
            if (text[c] == '\n' || text[c] == '\r') continue;
            if (characterPositions[focusBase + c].y > rowY + size * 0.3f) { nextRowTime = firstTimeOnRow; rowY = characterPositions[focusBase + c].y; }
            characterNextRows[focusBase + c] = nextRowTime;
            firstTimeOnRow = characterTimes[focusBase + c];
        }
        heights[index] = -y + size * 1.2f;
        layoutRows[index] = Mathf.RoundToInt(-y / (size * 1.5f)) + 1;
        layoutBuilt[index] = true;
        if (!makeMesh) return;
        int slot = index % lineMeshes.Length;
        Mesh mesh = buildingNativeOverview ? nativeOverviewScratch : lineMeshes[slot].sharedMesh;
        if (mesh == null) mesh = new Mesh(); else mesh.Clear();
        mesh.indexFormat = count * 8 > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = timing; mesh.uv3 = lineTiming;
        // Immutable paragraph metadata lets the shader retire previous paragraphs without frame allocations.
        var pivots = new Vector2[count * 8];
        var exits = new Vector2[count * 8];
        var history = new Vector2[count * 8];
        Vector2 pivot = new Vector2(position.x + widths[index] * 0.5f, position.y - heights[index] * 0.5f + size * 0.35f);
        float nextParagraph = 10000000f;
        float olderAt = 10000000f;
        for (int next = index + 1; next < texts.Length; next++)
        {
            if (string.IsNullOrEmpty(texts[next]) || texts[next].Trim().Length == 0) continue;
            if (nextParagraph >= 9999999f) nextParagraph = Mathf.Max(lineEnd, starts[next]);
            else { olderAt = Mathf.Max(nextParagraph, starts[next]); break; }
        }
        for (int vertex = 0; vertex < pivots.Length; vertex++) { pivots[vertex] = pivot; exits[vertex] = new Vector2(nextParagraph, 1); history[vertex] = new Vector2(olderAt, starts[index]); }
        mesh.uv4 = pivots; mesh.uv5 = exits; mesh.uv6 = history;
        var styling = new Vector2[vertices.Length];
        if (!keywordLayoutBuilt[index])
        {
            int[] keywords = MatchKeywordPalette(text);
            float[] ranked = RankKeywordPresentation(keywords);
            for (int c = 0; c < count; c++) { characterKeywordPalette[focusBase + c] = keywords[c]; characterKeywordStyle[focusBase + c] = ranked[c]; }
            keywordLayoutBuilt[index] = true;
        }
        for (int c = 0; c < count; c++) for (int v = 0; v < 8; v++) styling[c * 8 + v] = new Vector2(characterKeywordStyle[focusBase + c], refrain + presentation * 0.2f);
        mesh.uv7 = styling;
        var sectionTiming = new Vector2[vertices.Length];
        for (int v = 0; v < vertices.Length; v++) sectionTiming[v] = refrainWindows[index];
        mesh.uv8 = sectionTiming;
        mesh.colors32 = colors; mesh.triangles = triangles;
        mesh.RecalculateBounds();
        if (buildingNativeOverview) { nativeOverviewScratch = mesh; return; }
        lineMeshes[slot].sharedMesh = mesh;
        if (slotLines[slot] < 0) builtLineCount++;
        slotLines[slot] = index;
        lineMeshes[slot].gameObject.SetActive(true);
        if (statusText != null) statusText.text = "";
    }

    private int FindGlyph(int code)
    {
        int lo = 0, hi = glyphCodes.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (glyphCodes[mid] == code) return mid;
            if (glyphCodes[mid] < code) lo = mid + 1; else hi = mid - 1;
        }
        return -1;
    }

    private void SetQuad(Vector3[] vertices, Vector2[] uv, Color32[] colors, int offset,
        float x, float y, float width, float height, Vector2 low, Vector2 high, bool stamp)
    {
        vertices[offset] = new Vector3(x, y, stamp ? -0.01f : 0);
        vertices[offset + 1] = new Vector3(x, y + height, stamp ? -0.01f : 0);
        vertices[offset + 2] = new Vector3(x + width, y + height, stamp ? -0.01f : 0);
        vertices[offset + 3] = new Vector3(x + width, y, stamp ? -0.01f : 0);
        uv[offset] = low; uv[offset + 1] = new Vector2(low.x, high.y);
        uv[offset + 2] = high; uv[offset + 3] = new Vector2(high.x, low.y);
        Color32 color = new Color32(stamp ? (byte)255 : (byte)0, 255, 255, 255);
        for (int i = 0; i < 4; i++) colors[offset + i] = color;
    }

    // Shared by runtime playback and deterministic Editor verification.
    public void RenderAtTime(float time)
    {
        if (exiting || glyphMaterial == null || stageCamera == null || articleRoot == null) return;
        glyphMaterial.SetFloat("_SongTime", time);
        glyphMaterial.SetFloat("_ActiveColorStrength", lyrics != null && lyrics.wordHighlighting ? singingColorStrength : 0f);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_SongTime", time);
        int line = FindCameraLineAtTime(time);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_LyricFocusActive", 0);
        if (backgroundMaterial != null) { backgroundMaterial.SetFloat("_ReadingActivity", 0); backgroundMaterial.SetFloat("_RefrainAmount", 0); }
        if (!IsLineReady(line)) return;
        float end = ends[line] > starts[line] ? ends[line] : line + 1 < starts.Length ? starts[line + 1] : starts[line] + 5;
        float progress = Mathf.Clamp01((time - starts[line]) / Mathf.Max(end - starts[line], 0.05f));
        Vector2 p = positions[line];
        float targetSize = estimatedTiming ? (sizes[line] > normalFontSize ? 4.1f : 4.7f) : (sizes[line] > normalFontSize ? 3.5f : 3.7f);
        bool wrapped = layoutRows[line] > 1;
        float focusX = widths[line] * 0.5f;
        float focusY = -sizes[line] * 0.3f;
        int previousLayoutRow = focusedLayoutRow;
        float rowPreview = 0;
        focusedLayoutRow = 0;
        if (estimatedTiming)
        {
            // LRC has no word clock: frame the complete paragraph instead of inventing a reading cursor.
            focusY = -heights[line] * 0.5f + sizes[line] * 0.35f;
            targetSize = Mathf.Max(targetSize, heights[line] * 0.5f + 0.9f);
            float driftPhase = Mathf.Lerp(progress, progress - Mathf.Sin(progress * Mathf.PI * 2f) / (Mathf.PI * 2f), cameraRhythmStrength);
            focusX += (driftPhase - 0.5f) * Mathf.Min(widths[line] * 0.18f, 1.15f) * (wrapped ? wrappedMotionScale : 1f);
        }
        else
        {
            int first = characterOffsets[line];
            int character = 0;
            for (int c = 0; c < characterCounts[line]; c++)
            {
                if (texts[line][c] == '\r' || texts[line][c] == '\n') continue;
                if (characterTimes[first + c] > time) break;
                character = c;
            }
            Vector2 focus = characterCounts[line] == 0 ? Vector2.zero : characterPositions[first + character];
            // Interpolate the reading focus only. Glyph highlighting retains its original word clock.
            // Never interpolate across a row break: that return has a separate camera transition.
            int nextCharacter = character + 1;
            while (nextCharacter < characterCounts[line] && (texts[line][nextCharacter] == '\r' || texts[line][nextCharacter] == '\n')) nextCharacter++;
            if (nextCharacter < characterCounts[line])
            {
                Vector2 nextFocus = characterPositions[first + nextCharacter];
                float interval = characterTimes[first + nextCharacter] - characterTimes[first + character];
                if (Mathf.Abs(nextFocus.y - focus.y) < sizes[line] * 0.3f && interval > 0.001f)
                {
                    float phase = Mathf.Clamp01((time - characterTimes[first + character]) / interval);
                    focus = Vector2.Lerp(focus, nextFocus, phase);
                }
            }
            focusX = focus.x + 1.2f; focusY += focus.y;
            focusedLayoutRow = Mathf.RoundToInt(-focus.y / (sizes[line] * 1.5f));
            float density = 0;
            for (int c = Mathf.Max(0, character - 24); c < Mathf.Min(characterCounts[line], character + 25); c++)
            {
                if (texts[line][c] == '\r' || texts[line][c] == '\n' || texts[line][c] == ' ') continue;
                density += 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Abs(time - characterTimes[first + c]) - 0.2f) / 0.55f));
            }
            if (backgroundMaterial != null) backgroundMaterial.SetFloat("_ReadingActivity", Mathf.Clamp01(density / 8f) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - starts[line]) / 0.3f)) * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - end) / 0.45f))));
            // Widen before a real layout-row return, then release the extra space after arrival.
            if (characterCounts[line] > 0) rowPreview = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - characterNextRows[first + character] + 0.65f) / 0.65f));
            if (focusedLayoutRow > 0)
            {
                rowPreview = Mathf.Max(rowPreview, 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - characterRowStarts[first + character]) / 0.55f)));
            }
            // Blend the true reading position with a slow-fast-slow row trajectory.
            // Both trajectories move forward; the bounded blend does not retime highlighting.
            float rowLeft = widths[line], rowRight = 0;
            for (int c = 0; c < characterCounts[line]; c++)
            {
                if (char.IsWhiteSpace(texts[line][c])) continue;
                Vector2 at = characterPositions[first + c];
                if (Mathf.Abs(at.y - focus.y) > sizes[line] * 0.3f) continue;
                rowLeft = Mathf.Min(rowLeft, at.x); rowRight = Mathf.Max(rowRight, at.x);
            }
            float rowStart = characterRowStarts[first + character];
            float rowEnd = Mathf.Min(end, characterNextRows[first + character]);
            float rowPhase = Mathf.Clamp01((time - rowStart) / Mathf.Max(rowEnd - rowStart, 0.1f));
            float travelPhase = rowPhase - Mathf.Sin(rowPhase * Mathf.PI * 2f) / (Mathf.PI * 2f);
            if (cameraGroupFirst[line] != cameraGroupLast[line] && line != cameraGroupLast[line]) travelPhase = rowPhase;
            if (rowRight > rowLeft)
            {
                float cinematicX = Mathf.Lerp(rowLeft, rowRight, travelPhase) + 1.2f;
                float allowance = Mathf.Min(0.65f, targetSize * stageCamera.aspect * 0.12f);
                focusX = Mathf.Clamp(Mathf.Lerp(focusX, cinematicX, Mathf.Clamp01(cameraRhythmStrength)), focusX - allowance, focusX + allowance);
            }
        }
        // Center actual glyph extents independently of mesh pooling and alphabet baselines.
        float visualBottom = 1000000f, visualTop = -1000000f;
        int visualFirst = characterOffsets[line];
        for (int c = 0; c < characterCounts[line]; c++)
        {
            char letter = texts[line][c];
            if (letter == '\r' || letter == '\n' || letter == ' ' || letter == '\t') continue;
            if (!estimatedTiming && Mathf.Abs(characterPositions[visualFirst + c].y + focusedLayoutRow * sizes[line] * 1.5f) > sizes[line] * 0.3f) continue;
            Vector2 bounds = characterVisualBounds[visualFirst + c];
            if (bounds.y <= bounds.x) continue;
            visualBottom = Mathf.Min(visualBottom, bounds.x); visualTop = Mathf.Max(visualTop, bounds.y);
        }
        if (visualTop > visualBottom) focusY = (visualBottom + visualTop) * 0.5f;
        float phraseEnvelope = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - starts[line]) / 0.6f)) * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - end) / 0.7f)));
        Vector2 refrainWindow = refrainWindows[line];
        float refrainAmount = GetRefrainStrength(line) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - refrainWindow.x) / 0.6f)) * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - refrainWindow.y) / 0.9f)));
        targetSize += refrainAmount * 0.28f + rowPreview * 0.55f;
        if (cameraGroupFirst[line] >= 0 && cameraGroupFirst[line] != cameraGroupLast[line])
        {
            float boundaryWide = 1 - GetCameraParagraphEnvelope(line, time);
            targetSize += Mathf.Clamp01(paragraphCameraStrength) * (cameraGroupMode[line] == 1 ? 0.28f + boundaryWide * 0.1f : boundaryWide * 0.18f);
        }
        if (backgroundMaterial != null)
        {
            backgroundMaterial.SetFloat("_RefrainAmount", RefrainAmountAtTime(time));
            if (estimatedTiming) backgroundMaterial.SetFloat("_ReadingActivity", Mathf.Clamp01(texts[line].Length / Mathf.Max(end - starts[line], 1f) / 10f) * phraseEnvelope);
        }
        // Keep the left and right edges visible whenever a complete layout row fits in the viewport.
        if (backgroundMaterial != null && time >= starts[line] && time <= end)
        {
            float rowY = -focusedLayoutRow * sizes[line] * 1.5f;
            float left = widths[line], right = 0;
            if (!estimatedTiming)
            {
                int first = characterOffsets[line];
                for (int c = 0; c < characterCounts[line]; c++)
                {
                    if (texts[line][c] == '\r' || texts[line][c] == '\n' || texts[line][c] == ' ' || texts[line][c] == '\t') continue;
                    Vector2 at = characterPositions[first + c];
                    if (Mathf.Abs(at.y - rowY) > sizes[line] * 0.3f) continue;
                    left = Mathf.Min(left, at.x); right = Mathf.Max(right, at.x);
                }
            }
            else { left = 0; right = widths[line]; }
            float centerY = estimatedTiming ? p.y - heights[line] * 0.5f + sizes[line] * 0.45f : p.y + rowY + sizes[line] * 0.35f;
            backgroundMaterial.SetVector("_LyricFocus", new Vector4(p.x + (left + right) * 0.5f, centerY,
                Mathf.Max((right - left) * 0.5f + sizes[line] * 0.65f, sizes[line]), estimatedTiming ? heights[line] * 0.5f + sizes[line] * 0.2f : sizes[line] * 0.7f));
            backgroundMaterial.SetFloat("_LyricFocusActive", 1);
        }
        float halfWidth = targetSize * stageCamera.aspect;
        float minimumCenter = widths[line] + 0.55f - halfWidth;
        float maximumCenter = halfWidth - 0.55f;
        if (estimatedTiming)
        {
            if (minimumCenter <= maximumCenter) focusX = Mathf.Clamp(focusX, minimumCenter, maximumCenter);
        }
        else
        {
            // Long aligned rows follow singing; short rows retain comfortable margins.
            float readingHalfWidth = halfWidth * 0.62f;
            if (widths[line] <= readingHalfWidth * 2f - 1.1f)
                focusX = Mathf.Clamp(focusX, widths[line] + 0.55f - readingHalfWidth, readingHalfWidth - 0.55f);
            else focusX = Mathf.Clamp(focusX, 0.8f, Mathf.Max(0.8f, widths[line] - 0.8f));
        }
        // A complete short single row stays still; long timed rows retain reading motion.
        if (!estimatedTiming && !wrapped && widths[line] <= halfWidth * 1.05f) focusX = widths[line] * 0.5f;
        cameraLocalTarget = new Vector3(p.x + focusX, p.y + focusY, -10);
        cameraLocalTarget = GetParagraphCameraTarget(line, time, cameraLocalTarget);
        if (estimatedTiming && minimumCenter <= maximumCenter)
            cameraLocalTarget.x = Mathf.Clamp(cameraLocalTarget.x, p.x + minimumCenter, p.x + maximumCenter);
        if (!estimatedTiming && widths[line] <= halfWidth * 1.24f - 1.1f)
            cameraLocalTarget.x = Mathf.Clamp(cameraLocalTarget.x, p.x + widths[line] + 0.55f - halfWidth * 0.62f, p.x + halfWidth * 0.62f - 0.55f);
        // Reading stability controls damping rather than a dead zone that repeatedly stops and starts.
        Vector3 target = articleRoot.TransformPoint(cameraLocalTarget);
        bool discontinuity = (time < lastTime || time - lastTime > 0.5f) && !enterFromAmbient;
        bool scheduledFlight = flightDurations[line] > 0 && time >= flightStarts[line] && time < starts[line];
        float motionScale = wrapped ? wrappedMotionScale : 1f;
        float songDelta = Mathf.Max(0, time - lastTime);
        bool rowChange = !estimatedTiming && line == lastLine && focusedLayoutRow != previousLayoutRow;
        if (discontinuity) { bridgeStart = -100f; cameraVelocity = Vector3.zero; }
        else if ((line != lastLine && (lastLine >= 0 || enterFromAmbient)) || rowChange)
        {
            float distance = Vector3.Distance(cameraPosition, target);
            bool continuousLine = !rowChange && !scheduledFlight && !enterFromAmbient && lastLine >= 0 &&
                ((paragraphCameraStrength > 0 && cameraGroupFirst[line] == cameraGroupFirst[lastLine] && distance <= 8f) ||
                (cameraRhythmStrength > 0 && !estimatedTiming && sectionIds[line] == sectionIds[lastLine] && distance <= 3.5f)) && starts[line] - ends[lastLine] <= 0.5f;
            if (continuousLine) bridgeStart = -100f;
            else
            {
            bridgeDuration = Mathf.Min(1.15f, Mathf.Max(0.18f, (end - starts[line]) * 0.28f));
            if (enterFromAmbient) bridgeDuration = 0.65f;
            if (scheduledFlight) bridgeDuration = flightDurations[line];
            if (distance <= 6f && !scheduledFlight && !enterFromAmbient)
                bridgeDuration = Mathf.Min(0.42f, Mathf.Max(0.16f, (end - starts[line]) * 0.16f));
            if (rowChange) bridgeDuration = Mathf.Clamp(distance * 0.045f, 0.42f, 0.72f);
            Vector3 localTravel = articleRoot.InverseTransformPoint(target) - articleRoot.InverseTransformPoint(cameraPosition);
            // Size the overview to the actual two focus positions; a fixed zoom cap clips long column moves.
            bridgeLift = distance > 6f ? Mathf.Max(Mathf.Abs(localTravel.y) * 0.5f,
                Mathf.Abs(localTravel.x) / Mathf.Max(stageCamera.aspect * 2f, 0.1f)) + 0.75f : 0f;
            bridgeStart = scheduledFlight ? flightStarts[line] : time;
            bridgeFrom = cameraPosition; bridgeFromSize = cameraSize;
            cameraVelocity = Vector3.zero;
            if (enterFromAmbient) bridgeLift = Mathf.Max(0.001f, bridgeLift);
            }
        }
        enterFromAmbient = false;
        if (!discontinuity && time < bridgeStart + bridgeDuration)
        {
            float phase = Mathf.Clamp01((time - bridgeStart) / bridgeDuration);
            float eased = phase * phase * phase * (phase * (phase * 6f - 15f) + 10f);
            cameraPosition = Vector3.Lerp(bridgeFrom, target, eased);
            cameraSize = Mathf.Lerp(bridgeFromSize, targetSize, eased) + Mathf.Sin(phase * Mathf.PI) * bridgeLift;
        }
        else
        {
            // A stopped song clock freezes camera convergence as well as the shader animation.
            float blend = discontinuity ? 1f : 1f - Mathf.Exp(-songDelta * (lastLine == line ? 5f : 9f) * motionScale);
            if ((!estimatedTiming || paragraphCameraStrength > 0) && !discontinuity && songDelta > 0)
            {
                // Critically damped follow keeps velocity through successive character targets.
                // Song-clock delta freezes both position and velocity when paused.
                float rate = Mathf.Lerp(10f, 6f, Mathf.Clamp01(readingStability)) * motionScale;
                Vector3 error = cameraPosition - target;
                Vector3 impulse = cameraVelocity + error * rate;
                float decay = Mathf.Exp(-rate * songDelta);
                cameraPosition = target + (error + impulse * songDelta) * decay;
                cameraVelocity = (cameraVelocity - impulse * rate * songDelta) * decay;
            }
            else cameraPosition = Vector3.Lerp(cameraPosition, target, blend);
            cameraSize = Mathf.Lerp(cameraSize, targetSize, blend);
        }
        // Do not clamp the rendered position to each timed glyph: that bypassed smoothing,
        // especially when the next wrapped row restarted at its left edge.
        stageCamera.transform.position = cameraPosition;
        stageCamera.orthographicSize = cameraSize;
        lastLine = line; lastTime = time;
        ApplySongEnding(time, core == null ? 0 : core.Duration, lyrics == null ? 0 : lyrics.lyricOffsetSeconds, core != null && (core.Loop || core.IsRangeLooping));
        ApplyInterlude(time);
        UpdateBackgroundFrame();
    }

    public void ApplyInterlude(float time)
    {
        interludeAmount = 0;
        glyphMaterial.SetFloat("_InterludeAmount", 0);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_InterludeAmount", 0);
        if (lastLine < 0 || !IsLineReady(lastLine)) return;
        int next = lastLine + 1;
        while (next < texts.Length && (string.IsNullOrEmpty(texts[next]) || texts[next].Trim().Length == 0)) next++;
        // A final outro belongs to ApplySongEnding; do not infer an instrumental gap without an end time.
        if (next >= texts.Length || ends[lastLine] <= starts[lastLine] || starts[next] - ends[lastLine] < 7f) return;
        float fadeIn = Mathf.SmoothStep(0, 1, (time - ends[lastLine] - 0.65f) / 1.3f);
        float fadeOut = 1f - Mathf.SmoothStep(0, 1, (time - (starts[next] - 1.8f)) / 1.6f);
        interludeAmount = fadeIn * fadeOut;
        glyphMaterial.SetFloat("_InterludeAmount", interludeAmount);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_InterludeAmount", interludeAmount);
        Vector3 drift = articleRoot.TransformVector(new Vector3(Mathf.Sin(time * 0.19f) * 0.22f, Mathf.Sin(time * 0.13f) * 0.12f, 0));
        stageCamera.transform.position = cameraPosition + drift * interludeAmount;
        stageCamera.orthographicSize = cameraSize + interludeAmount * 0.35f;
    }

    // Duration is the player's media duration, never inferred from the last lyric timestamp.
    public void ApplySongEnding(float time, float mediaDuration, float lyricOffset, bool looping)
    {
        if (glyphMaterial == null || stageCamera == null || articleRoot == null) return;
        glyphMaterial.SetFloat("_EndingProgress", 0);
        glyphMaterial.SetFloat("_FinalLineHold", 0);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_EndingProgress", 0);
        float endingOpacity = 1;
        if (!looping && mediaDuration > 0 && mediaDuration < 86400)
            endingOpacity = 1 - Mathf.SmoothStep(0, 1, (time - (mediaDuration + lyricOffset - 0.35f)) / 0.35f);
        screenEndOpacity = endingOpacity;
        ApplyScreenFade();
        stageCamera.transform.position = cameraPosition;
        stageCamera.orthographicSize = cameraSize;
        if (looping || !(mediaDuration > 0 && mediaDuration < 86400) || !hasVisibleLyrics || lastLine < 0) return;
        int finalLine = texts.Length - 1;
        while (finalLine >= 0 && (string.IsNullOrEmpty(texts[finalLine]) || texts[finalLine].Trim().Length == 0)) finalLine--;
        if (finalLine < 0 || lastLine != finalLine || !IsLineReady(finalLine)) return;
        glyphMaterial.SetFloat("_FinalLineHold", time >= starts[finalLine] ? 1 : 0);
        float finish = mediaDuration + lyricOffset;
        float lyricEnd = ends[finalLine] > starts[finalLine] ? ends[finalLine] : starts[finalLine];
        float begin = Mathf.Max(finish - 4f, lyricEnd + 0.8f);
        // Very short/unknown outro: keep normal framing rather than rush a closing transition.
        if (finish - begin < 1f || time < begin) return;
        float phase = Mathf.Clamp01((time - begin) / (finish - begin));
        phase = phase * phase * (3f - 2f * phase);
        Vector3 center = new Vector3(positions[finalLine].x + widths[finalLine] * 0.5f,
            positions[finalLine].y - heights[finalLine] * 0.5f + sizes[finalLine] * 0.35f, -10);
        // Use the ordinary camera state as the base; repeated paused clocks never accumulate zoom.
        Vector3 baseLocal = articleRoot.InverseTransformPoint(cameraPosition);
        Vector3 movement = center - baseLocal;
        if (movement.magnitude > 1.5f) movement = movement.normalized * 1.5f;
        stageCamera.transform.position = articleRoot.TransformPoint(baseLocal + movement * phase * 0.45f);
        stageCamera.orthographicSize = cameraSize + phase * 1.1f;
        glyphMaterial.SetFloat("_EndingProgress", phase);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_EndingProgress", phase);
    }

    public int GetLayoutRowCount(int line) { return layoutRows[line]; }

    public void UpdateBackgroundFrame()
    {
        if (backgroundMaterial == null || articleRoot == null || stageCamera == null) return;
        Vector3 local = articleRoot.InverseTransformPoint(stageCamera.transform.position);
        // Orthographic screen rays intersect a fixed stage-local background plane.
        backgroundMaterial.SetVector("_StageView", new Vector4(local.x, local.y,
            stageCamera.orthographicSize * 2f * stageCamera.aspect, stageCamera.orthographicSize * 2f));
        UpdateSecondaryAnchors();
    }

    private void UpdateSecondaryAnchors()
    {
        float scale = stageCamera.orthographicSize;
        float depth = Mathf.Max(1f, stageCamera.nearClipPlane + 0.1f);
        if (translationMesh != null) { translationMesh.transform.localPosition = new Vector3(0, -scale * 0.82f, depth); translationMesh.transform.localScale = new Vector3(scale, scale, 1); }
        if (romanizationMesh != null) { romanizationMesh.transform.localPosition = new Vector3(0, scale * 0.82f, depth); romanizationMesh.transform.localScale = new Vector3(scale, scale, 1); }
    }

    private void HideSecondaryLabels()
    {
        if (translationMesh != null) translationMesh.gameObject.SetActive(false);
        if (romanizationMesh != null) romanizationMesh.gameObject.SetActive(false);
        displayedTranslation = ""; displayedRomanization = "";
    }

    public void UpdateSecondaryLabels(float time)
    {
        if (lyrics == null || displayMode != 2 || !hasVisibleLyrics) { HideSecondaryLabels(); return; }
        string translation = lyrics.TranslationAtTime(time), romanization = lyrics.RomanizationAtTime(time);
        if (translationMesh != null)
        {
            if (translation.Length == 0) { translationMesh.gameObject.SetActive(false); displayedTranslation = ""; }
            else if (translation != displayedTranslation || !translationMesh.gameObject.activeSelf)
            {
                translationGeometry = BuildSecondaryMesh(translationGeometry, translation, 0.064f);
                translationMesh.sharedMesh = translationGeometry; translationMesh.gameObject.SetActive(true); displayedTranslation = translation;
            }
        }
        if (romanizationMesh != null)
        {
            if (romanization.Length == 0) { romanizationMesh.gameObject.SetActive(false); displayedRomanization = ""; }
            else if (romanization != displayedRomanization || !romanizationMesh.gameObject.activeSelf)
            {
                romanizationGeometry = BuildSecondaryMesh(romanizationGeometry, romanization, 0.058f);
                romanizationMesh.sharedMesh = romanizationGeometry; romanizationMesh.gameObject.SetActive(true); displayedRomanization = romanization;
            }
        }
        UpdateSecondaryAnchors();
    }

    private Mesh BuildSecondaryMesh(Mesh mesh, string text, float size)
    {
        int count = Mathf.Min(text.Length, 256);
        var vertices = new Vector3[count * 4]; var uv = new Vector2[count * 4]; var colors = new Color32[count * 4];
        var timing = new Vector2[count * 4]; var lineData = new Vector2[count * 4]; var triangles = new int[count * 6];
        var rows = new int[count]; var widths = new float[2];
        float x = 0; int row = 0, used = 0;
        float limit = stageCamera.aspect * 1.72f;
        for (int c = 0; c < count; c++)
        {
            int code = text[c];
            if (code == '\r') continue;
            if (code == '\n') { if (row == 1) break; widths[row] = x; x = 0; row++; continue; }
            int glyph = FindGlyph(code); if (glyph < 0) glyph = FindGlyph(63); if (glyph < 0) continue;
            float advance = glyphAdvances[glyph] * size;
            if (x > 0 && x + advance > limit) { if (row == 1) break; widths[row] = x; row++; x = 0; }
            if ((row == 1 && x + advance * 2 > limit && c < text.Length - 1) || (c == count - 1 && count < text.Length))
            {
                int ellipsis = FindGlyph(0x2026); if (ellipsis >= 0) { glyph = ellipsis; advance = glyphAdvances[glyph] * size; }
                count = c + 1;
            }
            Vector4 metric = glyphMetrics[glyph], atlas = glyphUVs[glyph]; int v = used * 4;
            SetQuad(vertices, uv, colors, v, x + metric.x * size, -row * size * 1.5f + metric.y * size,
                metric.z * size, metric.w * size, new Vector2(atlas.x, atlas.y), new Vector2(atlas.z, atlas.w), false);
            for (int j = 0; j < 4; j++) { colors[v + j] = new Color32(0, 255, 0, code == ' ' || code == '\t' ? (byte)0 : (byte)104); timing[v + j] = new Vector2(0, 10000000); lineData[v + j] = new Vector2(10000000, 0); }
            rows[used] = row;
            int t = used * 6; triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2; triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            x += advance; used++;
        }
        widths[row] = x;
        for (int c = 0; c < used; c++) for (int j = 0; j < 4; j++) vertices[c * 4 + j] += new Vector3(-widths[rows[c]] * 0.5f, row * size * 0.75f - size * 0.35f, 0);
        if (mesh == null) mesh = new Mesh(); else mesh.Clear();
        mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = timing; mesh.uv3 = lineData; mesh.colors32 = colors; mesh.triangles = triangles; mesh.RecalculateBounds();
        return mesh;
    }

    public void RenderAmbientAtTime(float time)
    {
        if (stageCamera == null || articleRoot == null) return;
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_EndingProgress", 0);
        glyphMaterial.SetFloat("_EndingProgress", 0);
        glyphMaterial.SetFloat("_FinalLineHold", 0);
        interludeAmount = 0;
        glyphMaterial.SetFloat("_InterludeAmount", 0);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_InterludeAmount", 0);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_LyricFocusActive", 0);
        if (backgroundMaterial != null) { backgroundMaterial.SetFloat("_ReadingActivity", 0); backgroundMaterial.SetFloat("_RefrainAmount", 0); }
        glyphMaterial.SetFloat("_SongTime", time);
        if (backgroundMaterial != null) backgroundMaterial.SetFloat("_SongTime", time);
        cameraLocalTarget = new Vector3(Mathf.Sin(time * 0.13f) * 0.65f,
            Mathf.Sin(time * 0.09f) * 0.3f, -10);
        cameraPosition = articleRoot.TransformPoint(cameraLocalTarget);
        cameraSize = 4.9f + Mathf.Sin(time * 0.075f) * 0.12f;
        stageCamera.transform.position = cameraPosition;
        stageCamera.orthographicSize = cameraSize;
        lastTime = time; lastLine = -1; bridgeStart = -100f;
        UpdateBackgroundFrame();
    }

    private void RenderStatusMessage(string message, float time)
    {
        if (statusMesh == null) return;
        statusMesh.gameObject.SetActive(true);
        if (renderedStatus.Length > 0 && statusGeometry != null && statusGeometry.vertexCount == 4 && time >= statusClock) { renderedStatus = message; return; }
        renderedStatus = message; statusClock = time;
        // Reuse the original vector-rendered logo and Segoe title as one immutable image.
        // This avoids substituting the lyric font or changing the nested L proportions.
        var vertices = new Vector3[4]; var atlasUV = new Vector2[4]; var colors = new Color32[4];
        var timing = new Vector2[4]; var lineTiming = new Vector2[4];
        float height = 1.35f, width = height * (1159f / 327f);
        SetQuad(vertices, atlasUV, colors, 0, -width * 0.5f, -height * 0.5f, width, height, Vector2.zero, Vector2.one, true);
        for (int i = 0; i < 4; i++) { timing[i] = new Vector2(time - 0.1f, 10000000f); lineTiming[i] = new Vector2(10000000f, 3); }
        int[] triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        if (statusGeometry == null) statusGeometry = new Mesh(); else statusGeometry.Clear();
        Mesh mesh = statusGeometry;
        mesh.vertices = vertices; mesh.uv = atlasUV; mesh.uv2 = timing; mesh.uv3 = lineTiming;
        mesh.colors32 = colors; mesh.triangles = triangles; mesh.RecalculateBounds();
        statusMesh.sharedMesh = mesh;
    }

    public void ResetCover()
    {
        accentSum = Vector3.zero; accentWeight = 0;
        coverReady = false; coverHex = ""; coverSong = ""; coverPixels = null; coverCursor = 0;
        // The previous image fades to dark, but can never be promoted again by stale work.
    }

    public void ReceiveCoverData(DataDictionary data)
    {
        if (data == null) return;
        CheckSource();
        DataToken root, value;
        if (!data.TryGetValue("coverThumbnail", out root) || root.TokenType != TokenType.DataDictionary) return;
        DataDictionary thumbnail = root.DataDictionary;
        if (!thumbnail.TryGetValue("version", out value) || value.TokenType != TokenType.Double || value.Double != 1) return;
        if (!thumbnail.TryGetValue("width", out value) || value.TokenType != TokenType.Double || value.Double != 64) return;
        if (!thumbnail.TryGetValue("height", out value) || value.TokenType != TokenType.Double || value.Double != 64) return;
        if (!thumbnail.TryGetValue("encoding", out value) || value.TokenType != TokenType.String || value.String != "rgb24-hex-top-down") return;
        if (!thumbnail.TryGetValue("songId", out value) || value.TokenType != TokenType.String) return;
        string id = value.String;
        if (!data.TryGetValue("songId", out value) || value.TokenType != TokenType.String || value.String != id) return;
        if (CurrentCoverSongId() != id) return;
        if (!thumbnail.TryGetValue("pixels", out value) || value.TokenType != TokenType.String || value.String.Length != 64 * 64 * 6) return;
        // Missing/late lyric payloads must not erase this song's good or pending cover.
        ResetCover();
        coverHex = value.String; coverSong = id; coverPixels = new Color32[64 * 64];
    }

    private int HexDigit(char value)
    {
        if (value >= '0' && value <= '9') return value - '0';
        if (value >= 'a' && value <= 'f') return value - 'a' + 10;
        return -1;
    }

    private string CurrentCoverSongId()
    {
        string url = core == null || core.Url == null ? "" : core.Url.Get();
        if (!IsNetEaseUrl(url)) return "";
        url = url.ToLower().Replace("%3d", "=").Replace("%26", "&").Replace("%3f", "?");
        int start = url.IndexOf("?id=");
        if (start < 0) start = url.IndexOf("&id=");
        if (start < 0) return "";
        start += 4;
        int end = start;
        while (end < url.Length && url[end] >= '0' && url[end] <= '9') end++;
        return url.Substring(start, end - start);
    }

    public void BuildCoverStep()
    {
        if (backgroundMaterial == null) return;
        if (coverHex.Length > 0)
        {
            if (CurrentCoverSongId() != coverSong) { ResetCover(); return; }
            int limit = Mathf.Min(4096, coverCursor + 128);
            while (coverCursor < limit)
            {
                int offset = coverCursor * 6;
                int r1 = HexDigit(coverHex[offset]), r2 = HexDigit(coverHex[offset + 1]);
                int g1 = HexDigit(coverHex[offset + 2]), g2 = HexDigit(coverHex[offset + 3]);
                int b1 = HexDigit(coverHex[offset + 4]), b2 = HexDigit(coverHex[offset + 5]);
                if (r1 < 0 || r2 < 0 || g1 < 0 || g2 < 0 || b1 < 0 || b2 < 0) { ResetCover(); return; }
                int x = coverCursor % 64, y = 63 - coverCursor / 64;
                coverPixels[y * 64 + x] = new Color32((byte)(r1 * 16 + r2), (byte)(g1 * 16 + g2), (byte)(b1 * 16 + b2), 255);
                Vector3 sample = new Vector3((r1 * 16 + r2) / 255f, (g1 * 16 + g2) / 255f, (b1 * 16 + b2) / 255f);
                float peak = Mathf.Max(sample.x, Mathf.Max(sample.y, sample.z));
                float chroma = peak - Mathf.Min(sample.x, Mathf.Min(sample.y, sample.z));
                float weight = chroma * Mathf.Clamp01(peak * 4f) * Mathf.Clamp01((1f - peak) * 5f + 0.1f);
                accentSum += sample * weight; accentWeight += weight;
                coverCursor++;
            }
            // Complete the fade-out before replacing a texture still displayed by this material.
            if (coverCursor == 4096 && coverBlend <= 0.001f)
            {
                backgroundMaterial.SetTexture("_CoverTex", Texture2D.blackTexture);
                if (coverTexture != null) Destroy(coverTexture);
                coverTexture = new Texture2D(64, 64, TextureFormat.RGBA32, true);
                coverTexture.wrapMode = TextureWrapMode.Clamp; coverTexture.filterMode = FilterMode.Trilinear;
                coverTexture.SetPixels32(coverPixels); coverTexture.Apply();
                backgroundMaterial.SetTexture("_CoverTex", coverTexture);
                float accentPeak = Mathf.Max(accentSum.x, Mathf.Max(accentSum.y, accentSum.z));
                coverAccent = accentWeight > 0.01f && accentPeak > 0.001f ?
                    new Color(Mathf.Lerp(1, accentSum.x / accentPeak, 0.55f), Mathf.Lerp(1, accentSum.y / accentPeak, 0.55f), Mathf.Lerp(1, accentSum.z / accentPeak, 0.55f), 1) : Color.white;
                coverReady = true; coverHex = ""; coverPixels = null;
            }
        }
        coverBlend = Mathf.MoveTowards(coverBlend, coverReady && coverColors && lyrics != null && lyrics.coverEnabled ? 1 : 0, Time.unscaledDeltaTime * 2f);
        backgroundMaterial.SetFloat("_CoverBlend", coverBlend);
        backgroundMaterial.SetColor("_AccentColor", coverAccent);
        backgroundMaterial.SetFloat("_AccentBlend", coverBlend);
        glyphMaterial.SetColor("_AccentColor", coverAccent);
        glyphMaterial.SetFloat("_AccentBlend", coverBlend);
    }

    private void OnDestroy()
    {
        if (nativeOverviewScratch != null) Destroy(nativeOverviewScratch);
        RestoreLtcgiOutput();
        if (translationGeometry != null) Destroy(translationGeometry);
        if (romanizationGeometry != null) Destroy(romanizationGeometry);
        if (backgroundMaterial != null) { backgroundMaterial.SetFloat("_CoverBlend", 0); backgroundMaterial.SetTexture("_CoverTex", Texture2D.blackTexture); }
        if (coverTexture != null) Destroy(coverTexture);
        if (statusGeometry != null) Destroy(statusGeometry);
    }

    private void UpdateBackgroundAudio()
    {
        if (backgroundMaterial == null) return;
        // Adapter publishes only AudioLink currently owned by this VizVid source.
        // Recover old installations with a null reference, and late/runtime routing changes.
        if (core != null && core.AudioLink != null) backgroundAudioLink = (AudioLink.AudioLink)core.AudioLink;
        // VizVid's getter validates that AudioLink currently belongs to this player's active source.
        // Never fall back to the shared global shader texture or change AudioLink routing here.
        bool privateAnalysis = localAudio != null;
        bool ownsAudio = audioEffects && displayMode == 2 && eligibleSource && core != null && core.IsPlaying &&
            (privateAnalysis ? localAudio.playback == core && localAudio.isRunning && localAudio.output != null :
            backgroundAudioLink != null && backgroundAudioLink.isActiveAndEnabled &&
            core.AudioLink == backgroundAudioLink && backgroundAudioLink.audioRenderTexture != null);
        float now = Time.realtimeSinceStartup;
        float delta = Mathf.Clamp(now - lastBassUpdate, 0, 0.1f);
        lastBassUpdate = now;
        // Lost ownership must cut immediately, otherwise a release tail samples the other player's song.
        if (!ownsAudio) bassVisibility = 0;
        else bassVisibility = Mathf.MoveTowards(bassVisibility, 1, delta * 5f);
        backgroundMaterial.SetFloat("_BassEnabled", bassVisibility);
        if (ownsAudio) backgroundMaterial.SetTexture("_BassTexture", privateAnalysis ? localAudio.output : backgroundAudioLink.audioRenderTexture);
        // This world already enables AudioLink's CPU data. Do not enable a new readback path.
        bool hasData = ownsAudio && (privateAnalysis ? localAudio.hasData : backgroundAudioLink.AudioDataIsAvailable());
        Vector3 targets = Vector3.zero;
        if (hasData)
        {
            Vector3 raw = privateAnalysis ? localAudio.bands : new Vector3(
                backgroundAudioLink.GetDataAtPixel(0, 0).x * .65f + backgroundAudioLink.GetDataAtPixel(3, 28).x * .35f,
                backgroundAudioLink.GetDataAtPixel(0, 2).x * .60f + backgroundAudioLink.GetDataAtPixel(3, 30).x * .40f,
                backgroundAudioLink.GetDataAtPixel(0, 3).x * .80f + backgroundAudioLink.GetDataAtPixel(3, 31).x * .20f);
            targets.x = Mathf.Pow(Mathf.Clamp01(raw.x * 1.8f), .65f);
            targets.y = Mathf.Pow(Mathf.Clamp01(raw.y * 1.65f), .8f);
            targets.z = Mathf.Pow(Mathf.Clamp01(raw.z * 2.4f), .65f);
        }
        AdvanceReactiveBands(targets, delta, hasData);
        AdvanceHighMidRotation(reactiveBands.y, delta, hasData);
    }

    public void AdvanceReactiveBands(Vector3 target, float delta, bool hasOwnedData)
    {
        float dt = Mathf.Clamp(delta, 0, 0.1f);
        if (!hasOwnedData) reactiveBands = Vector3.zero;
        else
        {
            reactiveBands.x = Mathf.Lerp(reactiveBands.x, Mathf.Clamp01(target.x), 1 - Mathf.Exp(-dt / (target.x > reactiveBands.x ? 0.05f : 0.20f)));
            reactiveBands.y = Mathf.Lerp(reactiveBands.y, Mathf.Clamp01(target.y), 1 - Mathf.Exp(-dt / (target.y > reactiveBands.y ? 0.07f : 0.22f)));
            reactiveBands.z = Mathf.Lerp(reactiveBands.z, Mathf.Clamp01(target.z), 1 - Mathf.Exp(-dt / (target.z > reactiveBands.z ? 0.035f : 0.14f)));
        }
        if (backgroundMaterial != null) backgroundMaterial.SetVector("_SmoothedBands", new Vector4(reactiveBands.x, reactiveBands.y, reactiveBands.z, hasOwnedData ? 1 : 0));
    }

    public void AdvanceHighMidRotation(float amplitude, float delta, bool ownsAudio)
    {
        if (backgroundMaterial == null) return;
        if (ownsAudio && amplitude > 0.04f)
            highMidRotation = Mathf.Repeat(highMidRotation + Mathf.Clamp(amplitude - 0.04f, 0, 1.5f) * Mathf.Clamp(delta, 0, 0.1f) * 0.9f, Mathf.PI * 2);
        backgroundMaterial.SetFloat("_HighMidRotation", highMidRotation);
    }
}





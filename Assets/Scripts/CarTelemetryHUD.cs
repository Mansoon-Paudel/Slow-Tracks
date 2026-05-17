using UnityEngine;

public class CarTelemetryHUD : MonoBehaviour
{
    private CarController _car;
    private float _lapTime;
    private bool  _lapFinished;
    private GUIStyle _panel, _chipBox, _chipBoxDanger;
    private GUIStyle _speedLbl, _speedVal, _speedUnit;
    private GUIStyle _gearNum, _gearNumHot, _gearSub;
    private GUIStyle _timerLbl, _timerVal, _timerValDone;
    private GUIStyle _rpmLbl, _rpmVal;
    private GUIStyle _tileLbl, _tileVal;
    private GUIStyle _hint, _seg;
    private static readonly Color BG       = Color.black;
    private static readonly Color BG2      = Color.gray1;
    private static readonly Color BLUE_LO  = Color.softBlue;
    private static readonly Color BLUE_HI  = Color.dodgerBlue;
    private static readonly Color RED_HOT  = Color.softRed;
    private static readonly Color GREEN    = Color.darkSeaGreen;
    private static readonly Color RED_AIR  = Color.orangeRed;
    private static readonly Color MUTED    = Color.gray2;
    private void Start()
    {
        _car = FindAnyObjectByType<CarController>();
        if (_car == null) { Debug.LogError("HUD: CarController not found!"); enabled = false; }
    }
    private void Update()
    {
        if (!_lapFinished) _lapTime += Time.deltaTime;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!_lapFinished && other.CompareTag("End"))
            _lapFinished = true;
    }

    private GUIStyle Box(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply();
        return new GUIStyle { normal = { background = t } };
    }

    private GUIStyle Txt(int sz, Color col, bool bold = false,
        TextAnchor a = TextAnchor.MiddleLeft) => new GUIStyle
    {
        fontSize  = sz,
        fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
        alignment = a,
        normal    = { textColor = col }
    };

    private void EnsureStyles()
    {
        if (_panel != null) return;
        _panel          = Box(BG);
        _chipBox        = Box(BG2);
        _chipBoxDanger  = Box(Color.darkRed);
        _speedLbl       = Txt(9,  MUTED);
        _speedVal       = Txt(52, Color.white, bold: true);
        _speedUnit      = Txt(11, MUTED);
        _gearNum        = Txt(32, Color.white, bold: true, a: TextAnchor.MiddleCenter);
        _gearNumHot     = Txt(32, RED_HOT,     bold: true, a: TextAnchor.MiddleCenter);
        _gearSub        = Txt(9,  MUTED,  a: TextAnchor.MiddleCenter);
        _timerLbl       = Txt(8,  MUTED,  a: TextAnchor.MiddleCenter);
        _timerVal       = Txt(12, Color.white, bold: true, a: TextAnchor.MiddleCenter);
        _timerValDone   = Txt(12, GREEN,       bold: true, a: TextAnchor.MiddleCenter);
        _rpmLbl         = Txt(9,  MUTED);
        _rpmVal         = Txt(9,  MUTED, a: TextAnchor.MiddleRight);
        _tileLbl        = Txt(8,  MUTED);
        _tileVal        = Txt(12, Color.white, bold: true);
        _hint           = Txt(8,  Color.gray1, a: TextAnchor.MiddleCenter);
        _seg            = Box(Color.white);
    }
    private string FmtTime(float t)
    {
        int m = (int)(t / 60), s = (int)(t % 60), ms = (int)((t * 1000f) % 1000f);
        return $"{m:00}:{s:00}.{ms:000}";
    }

    private void OnGUI()
    {
        if (_car == null) return;
        EnsureStyles();

        float speed    = _car.SpeedKph;
        float rpm      = _car.EngineRpm;
        float rpmRatio = Mathf.Clamp01(rpm / 12500f);
        int   gear     = _car.CurrentGear;
        int   maxGear  = _car.MaxForwardGear;
        bool  top      = gear >= maxGear;
        bool  grounded = _car.IsGrounded;
        float load     = maxGear > 0 ? (float)gear / maxGear : 0f;

        const float PX = 14f, PY = 14f, PW = 440f, PH = 218f;
        const float PAD = 16f;
        GUI.Box(new Rect(PX, PY, PW, PH), GUIContent.none, _panel);
 
        float lx = PX + PAD;

        GUI.Label(new Rect(lx, PY + 12, 180, 12), "TELEMETRY", _speedLbl);
        GUI.Label(new Rect(lx, PY + 24, 200, 58), speed.ToString("F0"), _speedVal);
        GUI.Label(new Rect(lx, PY + 84, 80,  14), "KM / H", _speedUnit);

        const float CW = 90f;         
        float rx = PX + PW - PAD - CW; 

        Rect gchip = new Rect(rx, PY + 12, CW, 56);
        GUI.Box(gchip, GUIContent.none, top ? _chipBoxDanger : _chipBox);
        GUI.Label(new Rect(gchip.x, gchip.y + 4,  CW, 32), gear.ToString(),        top ? _gearNumHot : _gearNum);
        GUI.Label(new Rect(gchip.x, gchip.y + 38, CW, 14), $"GEAR {gear}/{maxGear}", _gearSub);

        Rect tchip = new Rect(rx, gchip.y + gchip.height + 5, CW, 42);
        GUI.Box(tchip, GUIContent.none, _lapFinished ? Box(Color.darkGreen) : _chipBox);
        GUI.Label(new Rect(tchip.x, tchip.y + 5,  CW, 12), "LAP TIME",         _timerLbl);
        GUI.Label(new Rect(tchip.x, tchip.y + 20, CW, 16), FmtTime(_lapTime),  _lapFinished ? _timerValDone : _timerVal);

         float rpmTop = PY + PH - PAD - 12 - 6 - 38 - 8 - 7 - 6 - 12;

        GUI.Label(new Rect(lx,          rpmTop, 60,         12), "RPM", _rpmLbl);
        GUI.Label(new Rect(rx - 10,     rpmTop, CW + 10,    12), $"{rpm:F0}", _rpmVal);

        float barY = rpmTop + 14;
        DrawSegBar(new Rect(lx, barY, PW - PAD * 2, 7), rpmRatio);
 
        float tileY = barY + 7 + 8;
        float tileW = (PW - PAD * 2 - 15) / 4f;

        DrawTile(new Rect(lx,                    tileY, tileW, 38), "ENGINE",    $"{rpm:F0}",          Color.white);
        DrawTile(new Rect(lx + (tileW + 5),      tileY, tileW, 38), "GRIP",     grounded ? "TRACK" : "AIR", grounded ? GREEN : RED_AIR);
        DrawTile(new Rect(lx + (tileW + 5) * 2,  tileY, tileW, 38), "LOAD",    $"{load * 100f:F0}%", Color.white);
        DrawTile(new Rect(lx + (tileW + 5) * 3,  tileY, tileW, 38), "SPEED",   $"{speed:F0}",        Color.white);

        float hintY = tileY + 38 + 6;
        GUI.Label(new Rect(lx, hintY, PW - PAD * 2, 12),
            "W · ACCEL   |   S · BRAKE/REV   |   A/D · STEER   |   SPACE · BRAKE", _hint); 
    }
    private void DrawSegBar(Rect r, float ratio)
    {
        const int SEGS = 22;
        float gap = 2f;
        float sw  = (r.width - gap * (SEGS - 1)) / SEGS;

        GUI.color = Color.gray1;
        GUI.Box(r, GUIContent.none, _seg);
        GUI.color = Color.white;
        for (int i = 0; i < SEGS; i++)
        {
            float lo = (float)i / SEGS, hi = (float)(i + 1) / SEGS;
            bool  lit = ratio >= lo, hot = hi > 0.82f, mid = hi > 0.50f;
            GUI.color = lit ? (hot ? RED_HOT : mid ? BLUE_HI : BLUE_LO) : Color.black;
            GUI.Box(new Rect(r.x + i * (sw + gap), r.y, sw, r.height), GUIContent.none, _seg);
        }
        GUI.color = Color.white;
    }
    private void DrawTile(Rect r, string lbl, string val, Color valCol)
    {
        GUI.Box(r, GUIContent.none, _chipBox);
        GUI.Label(new Rect(r.x + 6, r.y + 5,  r.width - 12, 11), lbl, _tileLbl);
        GUI.Label(new Rect(r.x + 6, r.y + 18, r.width - 12, 16), val, Txt(12, valCol, bold: true));
    }
}

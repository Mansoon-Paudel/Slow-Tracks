using System;
using NUnit.Framework;
using TMPro;using UnityEngine;

public class CarTelemetryHUD : MonoBehaviour
{
    private CarController _car;
    private float _lapTime;
    private bool _lapFinished;
    private float _bestLap = -1;
    [SerializeField] private Transform[] trackWaypoints;
    private Texture2D _minimapTex;
    private const int MAP_SIZE = 90;
    private GUIStyle _panel, _chipbox, _chipBoxDanger, _chipBoxDone;
    private GUIStyle _speedLbl, _speedVal, _speedUnit;
    private GUIStyle _gearNum, _gearNumHot, _guiStyle, _gearSub;
    private GUIStyle _timerLbl, _timerVal, _timerValDone;
    private GUIStyle _deltaLbl, _deltaVal, _deltaFaster, _deltaSlower;
    private GUIStyle _rpmLbl, _rpmVal, _tileLbl, _hint, _seg;
    private static readonly Color BG = Color.black;
    private static readonly Color BG2 = Color.gray2;
    private static readonly Color BLUE_LO = Color.lightSkyBlue;
    private static readonly Color BLUE_Hi = Color.deepSkyBlue;
    private static readonly Color RED_HOT = Color.orangeRed;
    private static readonly Color GREEN = Color.lightGreen;
    private static readonly Color RED_AIR = Color.indianRed;
    private static readonly Color MUTED = Color.gray;
    private void Start()
    {
        _car = FindAnyObjectByType<CarController>();
        if (_car == null)
        {
            Debug.Log("Car not found (HUD)");
            enabled = false;
            return;
        }

        BakeMinimapTexture();

    }

    private void Update()
    {
        if (!_lapFinished)
        {
            _lapTime += Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_lapFinished && other.CompareTag("End"))
        {
            _lapFinished = true;
            if (_bestLap < 0f || _lapTime < _bestLap)
            {
                _bestLap = _lapTime;
            }
        }
    }

    private void BakeMinimapTexture()
    {
        _minimapTex = new Texture2D(MAP_SIZE, MAP_SIZE, TextureFormat.RGBA32, false);
        var pixels = new Color[MAP_SIZE * MAP_SIZE];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        if (trackWaypoints == null || trackWaypoints.Length < 2)
        {
            _minimapTex.SetPixels(pixels);
            _minimapTex.Apply();
            return;
        }
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var wp in trackWaypoints)
        {
            if (wp == null)
            {
                continue;
            }

            minX = Mathf.Min(minX, wp.position.x);
            maxX = Mathf.Max(maxX, wp.position.x);
            minZ = Mathf.Min(minZ, wp.position.z);
            maxZ = Mathf.Max(maxZ, wp.position.z);
        }

        float rangeX = Mathf.Max(maxX - minX, 1);
        float rangeZ = Mathf.Max(maxZ - minZ, 1);
        const float PAD = 8f;

        Vector2 WorldToMap(Vector3 p) => new Vector2(
            PAD + (p.x - minX) / rangeX * (MAP_SIZE - PAD * 2),
            PAD + (p.z - minZ) / rangeZ * (MAP_SIZE - PAD * 2)
        );

        Color trackCol = Color.gray2;
        for (int i = 0; i < trackWaypoints.Length; i++)
        {
            if (trackWaypoints[i] == null)
            {
                continue;
            }
            int next = (i+1)% trackWaypoints.Length;
            if (trackWaypoints[next] == null)
            {
                continue;
            }
            Vector2 a = WorldToMap(trackWaypoints[i].position);
            Vector2 b = WorldToMap(trackWaypoints[next].position);
            DrawLine(pixels, a , b, trackCol, 4);
        }
        _minimapTex.SetPixels(pixels);
        _minimapTex.Apply();
}

    private void DrawLine(Color[] px, Vector2 a, Vector2 b, Color c, int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2);
        for (int s = 0; s <= steps; s++)
        {
            Vector2 p = Vector2.Lerp(a, b,  (float)s/steps);
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            int half = thickness / 2;
            for (int dx = -half; dx <= half; dx++)
                for (int dy = -half; dy <= half; dy++)
                {
                    int nx = cx + dx ,ny = cy + dy;
                    if (nx >= 0 && nx < MAP_SIZE && ny >= 0 && ny < MAP_SIZE)
                    {
                        px[ny*MAP_SIZE + nx] = c;
                    }
                }
        }
        
    }

    private void DrawCarDot(Rect mapRect)
    {
        if (trackWaypoints == null || trackWaypoints.Length < 2 || _car == null)
        {
            return;
        }
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var wp in trackWaypoints)
        {
            if (wp == null)
            {
                continue;
            }
            minX = Mathf.Min(minX, wp.position.x);
            maxX = Mathf.Max(maxX, wp.position.x);
            minZ = Mathf.Min(minZ, wp.position.z);
            maxZ = Mathf.Max(maxZ, wp.position.z);
        }
        float rangeX = Mathf.Max(maxX - minX, 1);
        float rangeZ = Mathf.Max(maxZ - minZ, 1);
        const float PAD = 8f;
        Vector3 cp = _car.transform.position;
        float nx = PAD + (cp.x - minX) / rangeX * (MAP_SIZE - PAD * 2);
        float ny = PAD + (cp.z - minZ) / rangeZ * (MAP_SIZE - PAD * 2);
        float px = mapRect.x + nx;
        float py = mapRect.y + (MAP_SIZE - ny);

        GUI.color = RED_HOT;
        GUI.DrawTexture(new Rect(px - 4, py - 4 , 8, 8),Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(px - 2, py - 2 , 4, 4),Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private GUIStyle Box(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return new GUIStyle
        {
            normal = {
                background = t
            }
        };
    }

    private GUIStyle Txt(int sz, Color col, bool bold = false, TextAnchor a = TextAnchor.MiddleLeft) => new GUIStyle
    {
        fontSize = sz,
        fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
        alignment = a,
        normal = { textColor = col }

    };
    private void EnsureStyles()
    {
        if (_panel != null) return;
        _panel         = Box(BG);
        _chipbox       = Box(BG2);
        _chipBoxDanger = Box(new Color(0.4f, 0.05f, 0.05f, 1f));
        _chipBoxDone   = Box(new Color(0.05f, 0.3f, 0.1f, 1f));
        _speedLbl      = Txt(9,  MUTED);
        _speedVal      = Txt(52, Color.white, bold: true);
        _speedUnit     = Txt(11, MUTED);
        _gearNum       = Txt(32, Color.white, bold: true, a: TextAnchor.MiddleCenter);
        _gearNumHot    = Txt(32, RED_HOT,     bold: true, a: TextAnchor.MiddleCenter);
        _gearSub       = Txt(9,  MUTED,  a: TextAnchor.MiddleCenter);
        _timerLbl      = Txt(8,  MUTED,  a: TextAnchor.MiddleCenter);
        _timerVal      = Txt(12, Color.white, bold: true, a: TextAnchor.MiddleCenter);
        _timerValDone  = Txt(12, GREEN,       bold: true, a: TextAnchor.MiddleCenter);
        _deltaLbl      = Txt(8,  MUTED,  a: TextAnchor.MiddleCenter);
        _deltaVal      = Txt(12, MUTED,       bold: true, a: TextAnchor.MiddleCenter);
        _deltaFaster   = Txt(12, GREEN,       bold: true, a: TextAnchor.MiddleCenter);
        _deltaSlower   = Txt(12, RED_HOT,     bold: true, a: TextAnchor.MiddleCenter);
        _rpmLbl        = Txt(9,  MUTED);
        _rpmVal        = Txt(9,  MUTED, a: TextAnchor.MiddleRight);
        _tileLbl       = Txt(8,  MUTED);
        _hint          = Txt(8,  new Color(0.35f, 0.35f, 0.35f, 1f), a: TextAnchor.MiddleCenter);
        _seg           = Box(Color.white);
    }

    private string FmtTime(float t)
    {
        int m = (int)(t / 60), s = (int)(t % 60), ms = (int)((t * 1000) % 1000);
        return $"{m:00}:{s:00}.{ms:000}";
    }
    private string FmtDelta(float delta)
    {
        string sign = delta < 0 ? "-" : "+";
        float abs = Mathf.Abs(delta);
        int s = (int)abs;
        int ms = (int)((abs * 1000) % 1000);
        return $"{sign}{s:00}.{ms:000}";
    }

    private void DrawCompass(Rect r)
    {
        GUI.Box(r,GUIContent.none,_chipbox);

        float cx = r.x + r.width / 2;
        float cy = r.y + r.height / 2;
        float radius = r.width / 2 - 8;
        float heading = _car.transform.eulerAngles.y;

        string[] cards = { "N", "E", "S", "W" };
        float[] angles = { 0, 90, 180, 270 };
        for (int i = 0; i < 4; i++)
        {
            float rad = (angles[i]-heading) * Mathf.Deg2Rad;
            float lx = cx +  Mathf.Sin(rad) * (radius - 2) - 6;
            float ly = cy - Mathf.Cos(rad) * (radius - 2) - 7;
            bool isN = i == 0;
            GUI.Label(new Rect(lx,ly,14,14),cards[i], Txt(9,isN? RED_HOT : MUTED,a:TextAnchor.MiddleCenter));
        }
        float northRad  = -heading * Mathf.Deg2Rad;
        float needleLen = radius - 10f;
        Vector2 tip  = new Vector2(cx + Mathf.Sin(northRad) *  needleLen,
            cy - Mathf.Cos(northRad) *  needleLen);
        Vector2 tail = new Vector2(cx + Mathf.Sin(northRad) * -needleLen,
            cy - Mathf.Cos(northRad) * -needleLen);

        GUI.color = MUTED;
        GUI.DrawTexture(new Rect(cx - 3, cy - 3, 6, 6), Texture2D.whiteTexture);
        GUI.color = RED_HOT;
        GUI.DrawTexture(new Rect(tip.x  - 3, tip.y  - 3, 6, 6), Texture2D.whiteTexture);
        GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        GUI.DrawTexture(new Rect(tail.x - 2, tail.y - 2, 4, 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x, r.y + r.height - 15f, r.width, 12f),
            $"{(int)heading:000}°", Txt(8, MUTED, a: TextAnchor.MiddleCenter));
    }

    private void DrawSteeringIndicator(Rect r, float steerInput)
    {
        GUI.Box(r, GUIContent.none, _chipbox);
        GUI.Label(new Rect(r.x, r.y + 5, r.width, 10), "STEER",
            Txt(8, MUTED, a: TextAnchor.MiddleCenter));

        float arcY = r.y + 18f;
        float arcH = r.height - 32f;
        float arcW = r.width - 12f;
        float arcX = r.x + 6f;
        float arcCX = arcX + arcW / 2f;
        float arcCY = arcY + arcH;
        float arcRadius = arcW / 2f;

        const int ARC_STEPS = 30;
        const float ARC_RANGE = 120;

        for (int i = 0; i < ARC_STEPS; i++)
        {
            float t0 = (float)i / ARC_STEPS;
            float t1 = (float)(i + 1) / ARC_STEPS;
            float a0 = (-ARC_RANGE / 2f + t0 * ARC_RANGE) * Mathf.Deg2Rad;
            float a1 = (-ARC_RANGE / 2f + t1 * ARC_RANGE) * Mathf.Deg2Rad;
            float x0 = arcCX + Mathf.Sin(a0) * arcRadius;
            float y0 = arcCY - Mathf.Cos(a0) * arcRadius;
            float x1 = arcCX + Mathf.Sin(a1) * arcRadius;
            float y1 = arcCY - Mathf.Cos(a1) * arcRadius;
            float dotX = (x0 + x1) / 2f, dotY = (y0 + y1) / 2f;
            GUI.color = Color.gray2;
            GUI.DrawTexture(new Rect(dotX - 1.5f, dotY - 1.5f, 3, 3), Texture2D.whiteTexture);
        }
        float needleAngle  = steerInput * (ARC_RANGE / 2f) * Mathf.Deg2Rad;
        float needleLength = arcRadius - 4f;
        float tipX = arcCX + Mathf.Sin(needleAngle) * needleLength;
        float tipY = arcCY - Mathf.Cos(needleAngle) * needleLength;
        Color needleCol = Mathf.Abs(steerInput) < 0.05f ? Color.white
            : steerInput < 0f ? BLUE_Hi : RED_HOT;

        const int NEEDLE_STEPS = 12;
        for (int i = 0; i <= NEEDLE_STEPS; i++)
        {
            float t  = (float)i / NEEDLE_STEPS;
            float px = Mathf.Lerp(arcCX, tipX, t);
            float py = Mathf.Lerp(arcCY, tipY, t);
            GUI.color = needleCol;
            GUI.DrawTexture(new Rect(px - 1.5f, py - 1.5f, 3, 3), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(arcCX - 3, arcCY - arcRadius - 3, 6, 6), Texture2D.whiteTexture);
        GUI.color = Color.white;

        int angleDeg = Mathf.RoundToInt(steerInput * ARC_RANGE / 2);
        string lbl     = Mathf.Abs(steerInput) < 0.02f ? "0°"
            : steerInput < 0f ? $"L {Mathf.Abs(angleDeg)}°" : $"R {angleDeg}°";
        GUI.Label(new Rect(r.x, r.y + r.height - 13f, r.width, 12f),
            lbl, Txt(8, MUTED, a: TextAnchor.MiddleCenter));
        
    }

    private void OnGUI()
    {
        if (_car == null) return;
        EnsureStyles();
        float speed = _car.SpeedKph;
        float rpm = _car.EngineRpm;
        float rpmRatio = Mathf.Clamp01(rpm / 12500);
        int gear = _car.CurrentGear;
        int maxGear = _car.MaxForwardGear;
        bool top = gear >= maxGear;
        bool grounded = _car.IsGrounded;
        float load = maxGear > 0 ? (float)gear / maxGear : 0;
        float steerInput = Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f);
        const float PX = 14f, PY = 14f, PW = 500f, PH = 230f, PAD = 16f;
        GUI.Box(new Rect(PX, PY, PW, PH), GUIContent.none, _panel);
        float lx = PX + PAD;
        
        GUI.Label(new Rect(lx, PY + 12, 180, 12), "TELEMETRY", _speedLbl);
        GUI.Label(new Rect(lx, PY + 24, 200, 58), speed.ToString("F0"), _speedVal);
        GUI.Label(new Rect(lx, PY + 84, 80,  14), "KM / H", _speedUnit);
        
        const float MW = MAP_SIZE, MH = MAP_SIZE;
        float mx = PX + PW - PAD - MW;
        float my = PY + 12;
        GUI.Label(new Rect(mx+18, my,60,12), "Track", _speedLbl);
        if (_minimapTex != null)
            GUI.DrawTexture(new Rect(mx, my + 14, MW, MH), _minimapTex);
        DrawCarDot(new Rect(mx, my + 14, MW, MH));

        float steerW = MW;
        float steerH = MH;
        DrawSteeringIndicator(new Rect(mx, my + 14 + MH + 4f, steerW, steerH), steerInput);
        const float CW = 90;
        float rx = mx - CW - 8;
        float speedBlockRight = lx + 100f;
        float compassSize     = 80f; 
        float gapCX = speedBlockRight + (rx - speedBlockRight) / 2f;
        float gapCY = PY + 14f + (90f / 2f) - (compassSize / 2f);
        DrawCompass(new Rect(gapCX - compassSize / 2f, gapCY, compassSize, compassSize));
        
        Rect gchip = new Rect(rx, PY + 12, CW, 52);
        GUI.Box(gchip, GUIContent.none, top ? _chipBoxDanger : _chipbox);
        GUI.Label(new Rect(gchip.x, gchip.y + 3,  CW, 32), gear.ToString(), top ? _gearNumHot : _gearNum);
        GUI.Label(new Rect(gchip.x, gchip.y + 37, CW, 12), $"GEAR {gear}/{maxGear}", _gearSub);

        Rect tchip = new Rect(rx, gchip.y + gchip.height + 4, CW, 40);
        GUI.Box(tchip, GUIContent.none, _lapFinished ? _chipBoxDone : _chipbox);
        GUI.Label(new Rect(tchip.x, tchip.y + 5,  CW, 12), "LAP TIME", _timerLbl);
        GUI.Label(new Rect(tchip.x, tchip.y + 19, CW, 16),
            FmtTime(_lapTime), _lapFinished ? _timerValDone : _timerVal);

        Rect dchip = new Rect(rx, tchip.y + tchip.height + 4, CW, 40);
        GUI.Box(dchip, GUIContent.none, _chipbox);
        GUI.Label(new Rect(dchip.x, dchip.y + 5,  CW, 12), "BEST LAP", _deltaLbl);
        if (_bestLap < 0f)
        {
            GUI.Label(new Rect(dchip.x, dchip.y + 19, CW, 16), "--:--.---", _deltaVal);
        }
        else if (!_lapFinished)
        {
            float liveDelta = _lapTime - _bestLap;
            GUI.Label(new Rect(dchip.x, dchip.y + 19, CW, 16),
                FmtDelta(liveDelta), liveDelta < 0f ? _deltaFaster : _deltaSlower);
        }
        else
        {
            GUI.Label(new Rect(dchip.x, dchip.y + 5,  CW, 12), "BEST LAP", _deltaLbl);
            GUI.Label(new Rect(dchip.x, dchip.y + 19, CW, 16), FmtTime(_bestLap), _deltaFaster);
        }

        float rpmTop = PY + PH - PAD - 12 - 6 - 38 - 8 - 7 - 6 - 12;
        float barW   = rx - lx - 8f;

        GUI.Label(new Rect(lx,      rpmTop, 60,        12), "RPM",        _rpmLbl);
        GUI.Label(new Rect(lx + 60, rpmTop, barW - 60, 12), $"{rpm:F0}", _rpmVal);

        float barY = rpmTop + 14;
        DrawSegBar(new Rect(lx, barY, barW, 7), rpmRatio);

        
        float tileY = barY + 7 + 8;
        float tileW = (barW - 15f) / 4f;
        DrawTile(new Rect(lx,                   tileY, tileW, 38), "ENGINE", $"{rpm:F0}",               Color.white);
        DrawTile(new Rect(lx + (tileW + 5),     tileY, tileW, 38), "GRIP",  grounded ? "TRACK" : "AIR", grounded ? GREEN : RED_AIR);
        DrawTile(new Rect(lx + (tileW + 5) * 2, tileY, tileW, 38), "LOAD",  $"{load * 100f:F0}%",      Color.white);
        DrawTile(new Rect(lx + (tileW + 5) * 3, tileY, tileW, 38), "SPEED", $"{speed:F0}",             Color.white);

        GUI.Label(new Rect(lx, tileY + 38 + 6, PW - PAD * 2, 12),
            "W · ACCEL   |   S · BRAKE/REV   |   A/D · STEER   |   SPACE · BRAKE", _hint);
    }

    private void DrawSegBar(Rect r, float ratio)
    {
        const int SEGS = 22;
        float gap = 2f, sw = (r.width - gap * (SEGS - 1)) / SEGS;
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        GUI.Box(r, GUIContent.none, _seg);
        GUI.color = Color.white;
        for (int i = 0; i < SEGS; i++)
        {
            float lo = (float)i / SEGS, hi = (float)(i + 1) / SEGS;
            bool  lit = ratio >= lo, hot = hi > 0.82f, mid = hi > 0.50f;
            GUI.color = lit ? (hot ? RED_HOT : mid ? BLUE_Hi : BLUE_LO) : Color.black;
            GUI.Box(new Rect(r.x + i * (sw + gap), r.y, sw, r.height), GUIContent.none, _seg);
        }
        GUI.color = Color.white;
    }
    private void DrawTile(Rect r, string lbl, string val, Color valCol)
    {
        GUI.Box(r, GUIContent.none, _chipbox);
        GUI.Label(new Rect(r.x + 6, r.y + 5,  r.width - 12, 11), lbl, _tileLbl);
        GUI.Label(new Rect(r.x + 6, r.y + 18, r.width - 12, 16), val, Txt(12, valCol, bold: true));
    }
}

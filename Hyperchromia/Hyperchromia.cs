using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HMLLibrary;

public class Hyperchromia : Mod
{
    Harmony harmony;
    public static System.Random rand = new System.Random();
    public static string prefix = "[Hyperchromia]: ";
    public static int startInd;
    public static int endInd;
    public static Dictionary<uint, SO_ColorValue> cachedColors;
    public static Color darkOutline = new Color(0.345f, 0.25f, 0.145f);
    public void Start()
    {
        cachedColors = new Dictionary<uint, SO_ColorValue>();
        if (RAPI.GetLocalPlayer() != null && ComponentManager<CanvasHelper>.Value != null && ComponentManager<CanvasHelper>.Value.GetMenu(MenuType.PaintMenu) != null)
            modifyPaintMenu();
        harmony = new Harmony("com.aidanamite.Hyperchromia");
        harmony.PatchAll();
        if (ComponentManager<CanvasHelper>.Value != null)
            Patch_UI.Start(ComponentManager<CanvasHelper>.Value);
        Debug.Log(prefix + "Mod has been loaded!");
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll(harmony.Id);
        if (ComponentManager<CanvasHelper>.Value != null)
        {
            Patch_UI.currentInfo.Destroy();
            if (ComponentManager<CanvasHelper>.Value.GetMenu(MenuType.PaintMenu) != null)
                unmodifyPaintMenu();
        }
        Debug.Log(prefix + "Mod has been unloaded!");
    }

    public static void LogTree(Transform transform)
    {
        Debug.Log(GetLogTree(transform));
    }

    public static string GetLogTree(Transform transform, string prefix = " -")
    {
        string str = "\n";
        if (transform.GetComponent<Behaviour>() == null)
            str += prefix + transform.name;
        else
            str += prefix + transform.name + ": " + transform.GetComponent<Behaviour>().GetType().Name;
        foreach (Transform sub in transform)
            str += GetLogTree(sub, prefix + "--");
        return str;
    }

    public static SO_ColorValue CreateColorFromId(uint colorIndex)
    {
        if (cachedColors.ContainsKey(colorIndex))
            return cachedColors[colorIndex];
        SO_ColorValue color = ScriptableObject.CreateInstance<SO_ColorValue>();
        color.uniqueColorIndex = colorIndex;
        byte[] values = BitConverter.GetBytes((int)colorIndex - ColorPicker.Colors.Length);
        color.paintColor = new Color(values[0] / 255f, values[1] / 255f, values[2] / 255f);
        color.buttonColor = color.paintColor;
        int min = Math.Min(Math.Min(values[0], values[1]), values[2]);
        int max = Math.Max(Math.Max(values[0], values[1]), values[2]);
        int range = max - min;

        float h, s, v;
        Color.RGBToHSV(new Color(values[0] / 255f, values[1] / 255f, values[2] / 255f), out h, out s, out v);
        //Debug.Log("fetched values " + h + " " + s + " " + v);
        int colorAmount = RoundOut(range / 255f, 6);
        //Debug.Log("color amount " + colorAmount);
        if (h < 1 / 6f)
        {
            //Debug.Log("1 color hue " + (h * 6) + " " + h);
            int i = RoundOut(h * 6, colorAmount);
            //Debug.Log("color i " + i);
            color.redCost = new Cost(null, colorAmount - i);
            color.yellowCost = new Cost(null, i);
            color.blueCost = new Cost(null, 0);
        }
        else if (h < 2 / 3f)
        {
            //Debug.Log("2 color hue " + (h * 2 - 1 / 3f) + " " + h);
            int i = RoundOut(h * 2 - 1 / 3f, colorAmount);
            //Debug.Log("color i " + i);
            color.redCost = new Cost(null, 0);
            color.yellowCost = new Cost(null, colorAmount - i);
            color.blueCost = new Cost(null, i);
        }
        else
        {
            //Debug.Log("3 color hue " + (h * 3 - 2) + " " + h);
            int i = RoundOut((float)Math.Round(h * 3 - 2,1), colorAmount);
            //Debug.Log("color i " + i);
            color.redCost = new Cost(null, i);
            color.yellowCost = new Cost(null, 0);
            color.blueCost = new Cost(null, colorAmount - i);
        }
        int j = RoundOut(range == 255 ? 0 : min / (255f - range), 6 - colorAmount);
        //Debug.Log("color j " + j);
        color.whiteCost = new Cost(null, j);
        color.blackCost = new Cost(null, 6 - colorAmount - j);
        Traverse.Create(color).Method("OnValidate").GetValue();

        cachedColors.Add(colorIndex, color);
        return color;
    }

    public static float Ratio(float val1, float val2, float val3)
    {
        return (float)Math.Pow(val2 / (val1 + val2), 2) - (float)Math.Pow((0.5 - val3 / 2) / 18, 0.6);
    }

    public static int RoundOut(float percent, int total)
    {
        if (percent <= 0.5)
            return (int)Math.Ceiling(total * percent);
        return (int)Math.Floor(total * percent);
    }

    public static float CorrectSat(float val)
    {
        if (val > 0)
            return (float)Math.Pow(val, 4);
        return -(float)Math.Pow(val, 2);
    }

    public static void modifyPaintMenu()
    {
        Transform menu = ComponentManager<CanvasHelper>.Value.GetMenu(MenuType.PaintMenu).menuObjects[0].transform;
        RectTransform back = menu.Find("BrownBackground") as RectTransform;
        back.offsetMax += new Vector2(250, 0);
        back.offsetMin -= new Vector2(250, 0);
        Transform OptCon = Traverse.Create(ComponentManager<Settings>.Value).Field("optionsCanvas").GetValue<GameObject>().transform.FindChildRecursively("OptionMenuParent").FindChildRecursively("Graphics").FindChildRecursively("Content");
        GameObject sliderPrefab = null;
        foreach (Transform transform in OptCon.transform)
        {
            if (transform.GetComponentInChildren<UISlider>() != null)
            {
                sliderPrefab = GameObject.Instantiate(transform.GetComponentInChildren<Slider>().gameObject, null, false);
                sliderPrefab.name = "SliderPrefab";
                Slider slide = sliderPrefab.GetComponent<Slider>();
                slide.onValueChanged = new Slider.SliderEvent();
                slide.minValue = 0;
                slide.maxValue = 255;
                slide.wholeNumbers = true;
                slide.value = 0;
                var trans = slide.transform as RectTransform;
                trans.localRotation = Quaternion.Euler(0, 0, 90);
                trans.sizeDelta *= new Vector2(1.3f, 1);
                trans.anchorMin = Vector2.one / 2;
                trans.anchorMax = Vector2.one / 2;
                break;
            }
        }
        RectTransform RGBP = new GameObject("Primary_RGBControlContainer").AddComponent<RectTransform>();
        RGBP.SetParent(menu, false);
        RGBP.anchorMax = new Vector2(0, 0.5f);
        RGBP.anchorMin = new Vector2(0, 0.5f);
        RGBP.anchoredPosition = new Vector2(-110, 0);
        GenerateControls(RGBP, sliderPrefab, true);
        RectTransform RGBS = new GameObject("Secondary_RGBControlContainer").AddComponent<RectTransform>();
        RGBS.SetParent(menu, false);
        RGBS.anchorMax = new Vector2(1, 0.5f);
        RGBS.anchorMin = new Vector2(1, 0.5f);
        RGBS.anchoredPosition = new Vector2(110, 0);
        GenerateControls(RGBS, sliderPrefab, false);
    }

    static HoverButton GenerateControls(Transform RGB, GameObject sliderPrefab, bool primary)
    {
        GameObject sliderRed = GameObject.Instantiate(sliderPrefab, RGB, false);
        sliderRed.name = "RedSlider";
        (sliderRed.transform as RectTransform).anchoredPosition = new Vector2(-80, 80);
        GameObject sliderGreen = GameObject.Instantiate(sliderPrefab, RGB, false);
        sliderGreen.name = "GreenSlider";
        (sliderGreen.transform as RectTransform).anchoredPosition = new Vector2(0, 80);
        GameObject sliderBlue = GameObject.Instantiate(sliderPrefab, RGB, false);
        sliderBlue.name = "BlueSlider";
        (sliderBlue.transform as RectTransform).anchoredPosition = new Vector2(80, 80);
        GameObject textRed = CreateText(RGB, -80, -120, "0", 30, ComponentManager<CanvasHelper>.Value.dropText.color, 45, 45, ComponentManager<CanvasHelper>.Value.dropText.font, "RedText");
        GameObject textGreen = CreateText(RGB, 0, -120, "0", 30, ComponentManager<CanvasHelper>.Value.dropText.color, 45, 45, ComponentManager<CanvasHelper>.Value.dropText.font, "GreenText");
        GameObject textBlue = CreateText(RGB, 80, -120, "0", 30, ComponentManager<CanvasHelper>.Value.dropText.color, 45, 45, ComponentManager<CanvasHelper>.Value.dropText.font, "BlueText");
        GameObject outline = CreateImage(RGB, 0, -200, darkOutline, 90, 50, "ButtonHighlight");
        outline.SetActive(true);
        GameObject colorDisplay = CreateImage(RGB, 0, -200, Color.black, 80, 40, "ColorDisplay");
        HoverButton selectionButton = colorDisplay.AddComponent<HoverButton>();
        selectionButton.onHover = delegate { outline.GetComponent<Image>().color = Color.white; };
        selectionButton.onClick.AddListener(delegate {
            Color color = colorDisplay.GetComponent<Image>().color;
            ColorButton temp = new ColorButton();
            temp.colorValues = CreateColorFromId(BitConverter.ToUInt32(new byte[] { (byte)(255 * color.r), (byte)(255 * color.g), (byte)(255 * color.b), 0 }, 0) + (uint)ColorPicker.Colors.Length);
            temp.colorValues.primaryColor = primary;
            (primary ? ComponentManager<ColorMenu>.Value.primaryColorPicker : ComponentManager<ColorMenu>.Value.secondaryColorPicker).OnButtonClick(temp);
        });
        selectionButton.onExit = delegate { outline.GetComponent<Image>().color = darkOutline; };

        sliderRed.GetComponent<Slider>().onValueChanged.AddListener((i) => { textRed.GetComponent<Text>().text = i.ToString(); Image j = colorDisplay.GetComponent<Image>(); j.color = new Color(i / 255, j.color.g, j.color.b); });
        sliderGreen.GetComponent<Slider>().onValueChanged.AddListener((i) => { textGreen.GetComponent<Text>().text = i.ToString(); Image j = colorDisplay.GetComponent<Image>(); j.color = new Color(j.color.r, i / 255, j.color.b); });
        sliderBlue.GetComponent<Slider>().onValueChanged.AddListener((i) => { textBlue.GetComponent<Text>().text = i.ToString(); Image j = colorDisplay.GetComponent<Image>(); j.color = new Color(j.color.r, j.color.g, i / 255); });

        return selectionButton;
    }

    public static void unmodifyPaintMenu()
    {
        GameObject menu = ComponentManager<CanvasHelper>.Value.GetMenu(MenuType.PaintMenu).menuObjects[0];
        RectTransform back = menu.transform.Find("BrownBackground") as RectTransform;
        back.offsetMax -= new Vector2(250, 0);
        back.offsetMin += new Vector2(250, 0);
        GameObject.Destroy(menu.transform.Find("Primary_RGBControlContainer").gameObject);
        GameObject.Destroy(menu.transform.Find("Secondary_RGBControlContainer").gameObject);
    }

    public static GameObject CreateText(Transform parent, float x, float y, string text_to_print, int font_size, Color text_color, float width, float height, Font font, string name = "Text")
    {
        GameObject UItextGO = new GameObject(name);
        UItextGO.transform.SetParent(parent, false);
        RectTransform trans = UItextGO.AddComponent<RectTransform>();
        trans.sizeDelta = new Vector2(width, height);
        trans.anchoredPosition = new Vector2(x, y);
        Text text = UItextGO.AddComponent<Text>();
        text.text = text_to_print;
        text.font = font;
        text.fontSize = font_size;
        text.color = text_color;
        Shadow shadow = UItextGO.AddComponent<Shadow>();
        shadow.effectColor = new Color();
        return UItextGO;
    }
    public static void AddTextShadow(GameObject textObject, Color shadowColor, Vector2 shadowOffset)
    {
        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = shadowOffset;
    }
    public static void CopyTextShadow(GameObject textObject, GameObject shadowSource)
    {
        Shadow sourcesShadow = shadowSource.GetComponent<Shadow>();
        if (sourcesShadow == null)
            sourcesShadow = shadowSource.GetComponentInChildren<Shadow>();
        AddTextShadow(textObject, sourcesShadow.effectColor, sourcesShadow.effectDistance);
    }

    public static GameObject CreateImage(Transform parent, float x, float y, Color color, float width, float height, string name = "Image")
    {
        GameObject UIimageGO = new GameObject(name);
        UIimageGO.transform.SetParent(parent, false);
        RectTransform trans = UIimageGO.AddComponent<RectTransform>();
        trans.sizeDelta = new Vector2(width, height);
        trans.anchoredPosition = new Vector2(x, y);
        Image image = UIimageGO.AddComponent<Image>();
        image.color = color;
        return UIimageGO;
    }

    public static string ColorValueInfo(SO_ColorValue color, string name)
    {
        if (color == null)
            return "<color=#C06C6C>No " + name + " Data</color>";
        string str = "<color=#7CF058>" + name + " Data</color>:\n";
        if (color.uniqueColorIndex < ColorPicker.Colors.Length)
            str += " - Vanilla Color";
        else
            str += " - Colors: " + (int)(color.buttonColor.r * 255)
                + ", " + (int)(color.buttonColor.g * 255)
                + ", " + (int)(color.buttonColor.b * 255);
        return str + "\n > <color=#" + Hex(color.buttonColor.r * 255) + Hex(color.buttonColor.g * 255) + Hex(color.buttonColor.b * 255) + ">███████</color>";
    }

    static string Hex(byte value)
    {
        return new string(new char[] { "0123456789ABCDEF"[value / 16], "0123456789ABCDEF"[value % 16] });
    }
    static string Hex(float value)
    {
        return Hex((byte)value);
    }
}

public static class ExtentionMethods
{
    public static List<int[]> Possibilities<T>(this List<T> items, int length)
    {
        int size = items.Count;
        List<int[]> possible = new List<int[]>();
        for (int i = 0; i < size; i++)
        {
            possible.Add(new int[] { i });
        }
        while (possible[0].Length < length)
        {
            List<int[]> temp = new List<int[]>();
            foreach (int[] item in possible)
            {
                for (int i = item[item.Length - 1]; i < size; i++)
                {
                    int[] newItem = new int[item.Length + 1];
                    item.CopyTo(newItem, 0);
                    newItem[item.Length] = i;
                    temp.Add(newItem);
                }
            }
            possible = temp;
        }
        return possible;
    }
}

[HarmonyPatch(typeof(ColorPicker), "GetColorFromUniqueIndex")]
public class Patch_GetBlockPaint
{
    static bool Prefix(ref uint uniqueColorIndex, ref SO_ColorValue __result)
    {
        if (ColorPicker.Colors == null || ColorPicker.Colors.Length == 0)
            ColorPicker.Colors = Resources.LoadAll<SO_ColorValue>("Colors");
        if (uniqueColorIndex < ColorPicker.Colors.Length)
            return true;
        __result = Hyperchromia.CreateColorFromId(uniqueColorIndex);
        return false;
    }
}

[HarmonyPatch]
public class Patch_UI
{
    public static InfoWindow currentInfo = null;
    static timeDiff diff = null;
    static Block last = null;
    static RaycastHit lastHit;

    [HarmonyPatch(typeof(CanvasHelper), "Start")]
    [HarmonyPostfix]
    public static void Start(CanvasHelper __instance)
    {
        currentInfo = new InfoWindow(__instance);
        diff = new timeDiff(0.5f, true, true);
    }

    [HarmonyPatch(typeof(CanvasHelper), "Update")]
    [HarmonyPostfix]
    public static void Update(Network_Player ___playerNetwork)
    {
        var progress = 0.2f;
        if (___playerNetwork != null && ___playerNetwork.PaintBrush != null)
            progress = diff.Update(!___playerNetwork.PaintBrush.gameObject.activeSelf) * 0.4f;
        currentInfo.rect.anchorMin = new Vector2(0.8f + progress, currentInfo.rect.anchorMin.y);
        currentInfo.rect.anchorMax = new Vector2(1 + progress, currentInfo.rect.anchorMax.y);
    }

    [HarmonyPatch(typeof(PaintBrush), "PreviewPaint")]
    [HarmonyPostfix]
    static void UpdatePreview(PaintBrush __instance, Block block, Network_Player ___playerNetwork)
    {
        if (block == last)
            return;
        last = block;
        int paintSide = 0;
        if (block != null)
        {
            Vector3 vector = Vector3.zero;
            PaintMode paintMode = block.buildableItem.settings_buildable.PaintMode;
            if (paintMode != PaintMode.CameraPosition)
            {
                if (paintMode == PaintMode.LocalNormal)
                {
                    vector = block.transform.InverseTransformDirection(lastHit.normal);
                }
            }
            else
            {
                vector = block.transform.InverseTransformPoint(___playerNetwork.CameraTransform.position);
            }
            switch (block.buildableItem.settings_buildable.PrimaryPaintAxis)
            {
                case Axis.X:
                    if (vector.x >= 0)
                    {
                        paintSide = 1;
                        break;
                    }
                    paintSide = 2;
                    break;
                case Axis.Y:
                    if (vector.y <= 0)
                    {
                        paintSide = 1;
                        break;
                    }
                    paintSide = 2;
                    break;
                case Axis.Z:
                    if (vector.z >= 0)
                    {
                        paintSide = 1;
                        break;
                    }
                    paintSide = 2;
                    break;
                case Axis.NX:
                    if (vector.x < 0)
                    {
                        paintSide = 1;
                        break;
                    }
                    paintSide = 2;
                    break;
                case Axis.NY:
                    if (vector.y > 0)
                    {
                        paintSide = 1;
                        break;
                    }
                    paintSide = 2;
                    break;
                case Axis.NZ:
                    if (vector.z < 0)
                    {
                        paintSide = 1;
                        break;
                    }
                    paintSide = 2;
                    break;
                case Axis.All:
                    paintSide = 3;
                    break;
                case Axis.None:
                    paintSide = 0;
                    break;
                default:
                    paintSide = 3;
                    break;
            }
            SO_ColorValue color1;
            SO_ColorValue color2;
            uint pattern = uint.MaxValue;
            if (paintSide == 2)
            {
                color1 = PaintBrush.PreviewData.colorB;
                color2 = PaintBrush.PreviewData.patternColorB;
                if (color1 != color2)
                    pattern = PaintBrush.PreviewData.patternIndex2;
            }
            else
            {
                color1 = PaintBrush.PreviewData.colorA;
                color2 = PaintBrush.PreviewData.patternColorA;
                if (color1 != color2)
                    pattern = PaintBrush.PreviewData.patternIndex1;
            }
            currentInfo.display.text = "Data\n--------\n"
                + Hyperchromia.ColorValueInfo(color1, "Primary")
                + "\n\n" + (pattern == uint.MaxValue ? "<color=#C06C6C>No pattern</color>" : (Hyperchromia.ColorValueInfo(color2, "Secondary")
                + "\n\n<color=#7CF058>Pattern Index</color>: " + pattern));
        }
        else
            currentInfo.display.text = "No Data";
    }

    [HarmonyPatch(typeof(PaintBrush), "GetPaintSideFromPaintAxis")]
    [HarmonyPrefix]
    static void GetPaintSide(RaycastHit hit)
    {
        lastHit = hit;
    }

    [HarmonyPatch(typeof(PaintBrush), "ColorPickFromBlock")]
    [HarmonyPostfix]
    static void ColorPickFromBlock(ColorMenu ___colorMenu, Block block, int paintSide)
    {
        if (paintSide == 2 && block.currentColorB == null && block.currentPatternColorB == null)
            return;
        else if (paintSide != 2 && block.currentColorA == null && block.currentPatternColorA == null)
            return;
        uint num = 0;
        if (paintSide == 2)
            num = block.currentPatternIndex2;
        else
            num = block.currentPatternIndex1;
        ___colorMenu.SelectPatternButtonFromIndex(num);
    }
}

[HarmonyPatch(typeof(ColorPicker), "SelectButtonFromColor")]
public class Patch_SelectColor
{
    static bool Prefix(ColorPicker __instance, SO_ColorValue colorValue)
    {
        if (colorValue.uniqueColorIndex < ColorPicker.Colors.Length)
            return true;
        ColorButton temp = new ColorButton();
        temp.colorValues = colorValue;
        __instance.OnButtonClick(temp);
        return false;
    }
}

[HarmonyPatch(typeof(GameMenu), "Initialize")]
public class Patch_MenuCreate
{
    static void Postfix(ref GameMenu __instance)
    {
        if (__instance.menuType == MenuType.PaintMenu)
            Hyperchromia.modifyPaintMenu();
    }
}

class HoverButton : Button
{
    public Action onHover;
    public Action onExit;
    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        onHover?.Invoke();
    }
    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        onExit?.Invoke();
    }
}

public class InfoWindow
{
    public GameObject gameObject;
    public RectTransform rect;
    public Text display;
    public InfoWindow(CanvasHelper canvas)
    {
        Transform OptionMenuContainer = Traverse.Create(ComponentManager<Settings>.Value).Field("optionsCanvas").GetValue<GameObject>().transform.FindChildRecursively("OptionMenuParent");
        GameObject backgroundImg = OptionMenuContainer.transform.FindChildRecursively("BrownBackground").gameObject;

        gameObject = new GameObject("PaintInfoWindow");
        rect = gameObject.AddComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0.8f,0.65f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var background = GameObject.Instantiate(backgroundImg, rect, false).GetComponent<RectTransform>();
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        display = CreateText(rect, 0, 0, "No Data", canvas.dropText.fontSize, canvas.dropText.color, 1, 1, canvas.dropText.font).GetComponent<Text>();
        RectTransform textBox = display.GetComponent<RectTransform>();
        textBox.offsetMin = new Vector2(10, 10);
        textBox.offsetMax = new Vector2(-10, -10);
    }

    public void Destroy()
    {
        if (gameObject != null)
            GameObject.Destroy(gameObject);
    }

    static GameObject CreateText(Transform canvas_transform, float x, float y, string text_to_print, int font_size, Color text_color, float width, float height, Font font, string name = "Text")
    {
        GameObject UItextGO = new GameObject("Text");
        UItextGO.transform.SetParent(canvas_transform, false);
        RectTransform trans = UItextGO.AddComponent<RectTransform>();
        trans.anchorMin = new Vector2(x, y);
        trans.anchorMax = trans.anchorMin + new Vector2(width, height);
        Text text = UItextGO.AddComponent<Text>();
        text.text = text_to_print;
        text.font = font;
        text.fontSize = font_size;
        text.color = text_color;
        text.name = name;
        Shadow shadow = UItextGO.AddComponent<Shadow>();
        shadow.effectColor = new Color();
        return UItextGO;
    }
}

public class timeDiff
{
    double value;
    public double maxTime;
    bool gradient;
    float progress
    {
        get
        {
            if (gradient)
                return (float)(0.5 - Math.Cos(Math.PI * value / maxTime) / 2);
            return (float)(value / maxTime);
        }
    }
    public timeDiff(double maxTime, bool useGradient = false, bool startAtMax = false)
    {
        this.maxTime = maxTime;
        gradient = useGradient;
        value = startAtMax ? maxTime : 0;
    }
    public float Update(bool toMax)
    {
        float timePassed = Time.deltaTime;
        value = Math.Min(maxTime, Math.Max(0, value + (toMax ? timePassed : -timePassed)));
        return progress;
    }
}
namespace AdaptiveRoads.UI.RoadEditor {
    using ColossalFramework.UI;
    using KianCommons;
    using KianCommons.UI;
    using System;
    using UnityEngine;
    using AdaptiveRoads.Util;
    using System.Reflection;
    using System.Linq;
    using KianCommons.UI.Helpers;
    using AdaptiveRoads.Manager;
    using static KianCommons.ReflectionHelpers;


    public class BitMaskPanel : UIPanel, IDataUI {
        public UILabel Label;
        public UICheckboxDropDown DropDown;

        public delegate void SetHandlerD(int value);
        public delegate int GetHandlerD();
        public SetHandlerD SetHandler;
        public GetHandlerD GetHandler;
        public Type EnumType;
        public string Hint;
        public event REPropertySet.PropertyChangedHandler EventPropertyChanged;

        public override void OnDestroy() {
            ReflectionHelpers.SetAllDeclaredFieldsToNull(this);
            base.OnDestroy();
        }

        public static BitMaskPanel Add(
            RoadEditorPanel roadEditorPanel,
            UIComponent container,
            string label,
            Type enumType,
            SetHandlerD setHandler,
            GetHandlerD getHandler,
            string hint) {
            Log.Debug($"BitMaskPanel.Add(container:{container}, label:{label}, enumType:{enumType})");
            var subPanel = UIView.GetAView().AddUIComponent(typeof(BitMaskPanel)) as BitMaskPanel;
            subPanel.EnumType = enumType;
            subPanel.SetHandler = setHandler;
            subPanel.GetHandler = getHandler;
            subPanel.Initialize();
            subPanel.Label.text = label + ":";
            subPanel.Hint = hint;
            //if (dark)
            //    subPanel.opacity = 0.1f;
            //else
            //    subPanel.opacity = 0.3f;

            container.AttachUIComponent(subPanel.gameObject);
            roadEditorPanel.FitToContainer(subPanel);

            return subPanel;
        }

        public override void Awake() {
            base.Awake();
            size = new Vector2(370, 54);
            atlas = TextureUtil.Ingame;
            //backgroundSprite = "GenericPanelWhite";
            //color = Color.white;

            Label = AddUIComponent<UILabel>();
            Label.relativePosition = new Vector2(0, 6);

            DropDown = AddUIComponent<UICheckboxDropDown>();
            EditorMultiSelectDropDown.Init(DropDown);
            DropDown.relativePosition = new Vector2(width - DropDown.width, 28);
            DropDown.eventAfterDropdownClose += DropdownClose;
        }

        private void Initialize() {
            //Disable();
            Populate(DropDown, GetHandler(), EnumType);
            UpdateText();
            Enable();
        }

        public void Refresh() => Initialize();

        public static void Populate(UICheckboxDropDown dropdown, int flags, Type enumType) {
            var values = EnumBitMaskExtensions.GetPow2ValuesI32(enumType);
            foreach(int flag in values) {
                bool hasFlag = (flags & flag) != 0;

                // TODO hide lane flags based on set/get.
                var itemInfo = enumType.GetEnumMember(flag);
                bool hide = itemInfo.HasAttribute<HideAttribute>();
                hide &= ModSettings.HideIrrelavant;
                hide &= !hasFlag;
                if(hide)
                    continue; // hide

                dropdown.AddItem(
                    item: Enum.GetName(enumType, flag),
                    isChecked: hasFlag,
                    userData: flag);
            }
        }


        public override void Start() {
            base.Start();
            UIButton button = DropDown.triggerButton as UIButton;
        }

        private void DropdownClose(UICheckboxDropDown checkboxdropdown) {
            SetValue(GetCheckedFlags());
            UpdateText();
            UIButton button = DropDown.triggerButton as UIButton;
        }

        // apply checked flags from UI to prefab
        protected void SetValue(int value) {
            if(GetHandler() != value) {
                SetHandler(value);
                EventPropertyChanged?.Invoke();
            }
        }

        // get checked flags in UI
        private int GetCheckedFlags() {
            int ret = 0;
            for(int i = 0; i < DropDown.items.Length; i++) {
                if(DropDown.GetChecked(i)) {
                    ret |= (int)DropDown.GetItemUserData(i);
                }
            }
            return ret;
        }

        private void UpdateText() {
            int flags = GetCheckedFlags();
            string text = Enum.Format(enumType: EnumType, value: flags, "G");
            ApplyText(DropDown, text);
        }

        // private UIFontRenderer ObtainTextRenderer()
        static MethodInfo mObtainTextRenderer = GetMethod(typeof(UIButton), "ObtainTextRenderer");
        static UIFontRenderer ObtainTextRenderer(UIButton button) =>
            mObtainTextRenderer.Invoke(button, null) as UIFontRenderer;

        public static void ApplyText(UICheckboxDropDown dd, string text) {
            UIButton uibutton = (UIButton)dd.triggerButton;
            var padding = uibutton.textPadding;
            padding.left = 5;
            padding.right = 21;

            uibutton.text = text; // must set text to mearure text once and only once.

            using(UIFontRenderer uifontRenderer = ObtainTextRenderer(uibutton)) {
                float p2uRatio = uibutton.GetUIView().PixelsToUnits();
                var widths = uifontRenderer.GetCharacterWidths(text);
                float x = widths.Sum() / p2uRatio;
                //Log.Debug($"{uifontRenderer}.GetCharacterWidths(\"{text}\")->{widths.ToSTR()}");
                //if (x > uibutton.width - 42) 
                //    uibutton.textHorizontalAlignment = UIHorizontalAlignment.Left;
                //else
                //    uibutton.textHorizontalAlignment = UIHorizontalAlignment.Center;

                if(x > uibutton.width - uibutton.textPadding.horizontal) {
                    for(int n = 4; n < text.Length; ++n) {
                        float x2 = widths.Take(n).Sum() / p2uRatio + 15; // 15 = width of ...
                        if(x2 > uibutton.width - 21) {
                            text = text.Substring(0, n - 1) + "...";
                            break;
                        }
                    }

                }
            }
            uibutton.text = text;
        }

        public bool IsHovered() {
            if(containsMouse)
                return true;
            if(DropDown.GetHoverIndex() >= 0)
                return true;
            return false;
        }

        public string GetHint() {
            int i = DropDown.GetHoverIndex();
            if(DropDown.GetItemUserData(i) is int flag) {
                return EnumType.GetEnumMember(flag).GetHints().JoinLines();
            }
            if(DropDown.containsMouse || Label.containsMouse) {
                return Hint;
            }

            return null;
        }

    }
}

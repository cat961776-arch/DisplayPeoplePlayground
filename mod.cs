using System;
using System.Collections.Generic;
using Activations;
using UnityEngine;
using UnityEngine.Events;

namespace Mod
{
    public class Mod : MonoBehaviour
    {
        public static string ModTag = " [TxtSig]";

        public static void Main()
        {
            ModAPI.Register(
                new Modification()
                {
                    OriginalItem = ModAPI.FindSpawnable("Text Display"),
                    NameOverride = "Text Signal" + ModTag,
                    DescriptionOverride = "Stores a custom text. Right-click to edit it. Wire an activator (Radio, button, lever, etc.) into this item, and wire this item into a separate Text Display. When activated, it overwrites the connected Text Display's text with the stored text.",
                    CategoryOverride = ModAPI.FindCategory("Machinery"),
                    ThumbnailOverride = ModAPI.LoadSprite("sprites/placeholder.png"),
                    AfterSpawn = (Instance) =>
                    {
                        var oldDisplay = Instance.GetComponent<DisplayBehaviour>();
                        if (oldDisplay != null)
                            UnityEngine.Object.Destroy(oldDisplay);

                        var signal = Instance.GetOrAddComponent<TextSignalBehaviour>();
                        if (string.IsNullOrEmpty(signal.DisplayText))
                            signal.DisplayText = "Hello World!";
                    }
                }
            );

            ModAPI.Register(
                new Modification()
                {
                    OriginalItem = ModAPI.FindSpawnable("Text Display"),
                    NameOverride = "Value Display" + ModTag,
                    DescriptionOverride = "Shows a live value read straight off this object's physics state. Right-click to toggle between Electricity (Charge) and Heat (Temperature), and to edit the unit suffix (defaults to W and \u00b0C). Wire a Copper Wire to a battery/power source for electricity, or a Heat Pipe to something hot, and the value updates automatically \u2014 no signal needed.",
                    CategoryOverride = ModAPI.FindCategory("Machinery"),
                    ThumbnailOverride = ModAPI.LoadSprite("sprites/placeholder.png"),
                    AfterSpawn = (Instance) =>
                    {
                        Instance.GetOrAddComponent<ValueDisplayBehaviour>();
                    }
                }
            );

            ModAPI.Register(
                new Modification()
                {
                    OriginalItem = ModAPI.FindSpawnable("Text Display"),
                    NameOverride = "If Display" + ModTag,
                    DescriptionOverride = "Shows different text automatically as its live value (Electricity/Heat, same as Value Display) rises or falls. Right-click to toggle mode and to edit your rules as \"threshold:text\" pairs separated by commas, e.g. 0:Cold,50:Warm,100:Hot \u2014 add as many as you want. Whichever threshold the current value has most recently passed decides what's shown; no signal or wire trigger needed, it just watches the value.",
                    CategoryOverride = ModAPI.FindCategory("Machinery"),
                    ThumbnailOverride = ModAPI.LoadSprite("sprites/placeholder.png"),
                    AfterSpawn = (Instance) =>
                    {
                        var ifDisplay = Instance.GetOrAddComponent<IfDisplayBehaviour>();
                        if (ifDisplay.Rules.Count == 0)
                        {
                            ifDisplay.Rules.Add(new ThresholdRule { Threshold = 0f, Text = "Cold" });
                            ifDisplay.Rules.Add(new ThresholdRule { Threshold = 50f, Text = "Warm" });
                            ifDisplay.Rules.Add(new ThresholdRule { Threshold = 100f, Text = "Hot" });
                        }
                    }
                }
            );
        }
    }

    public enum ValueDisplayMode
    {
        Electricity,
        Heat
    }

    public class ValueDisplayBehaviour : MonoBehaviour
    {
        public ValueDisplayMode Mode = ValueDisplayMode.Electricity;
        public string ElectricitySuffix = "W";
        public string HeatSuffix = "\u00b0C";

        [SkipSerialisation]
        private PhysicalBehaviour physicalBehaviour;

        [SkipSerialisation]
        private DisplayBehaviour displayBehaviour;

        [SkipSerialisation]
        private bool contextMenuRegistered;

        [SkipSerialisation]
        private string lastRenderedText;

        private void Awake()
        {
            physicalBehaviour = GetComponent<PhysicalBehaviour>();
            displayBehaviour = GetComponent<DisplayBehaviour>();
        }

        private void Start()
        {
            RegisterContextMenu();
        }

        private void RegisterContextMenu()
        {
            if (contextMenuRegistered)
                return;

            if (physicalBehaviour == null)
                physicalBehaviour = GetComponent<PhysicalBehaviour>();

            if (physicalBehaviour == null || physicalBehaviour.ContextMenuOptions == null)
                return;

            physicalBehaviour.ContextMenuOptions.Buttons.Add(
                new ContextMenuButton(
                    "toggleValueDisplayMode",
                    "Toggle mode (Electricity/Heat)",
                    "Switch what this display reads: electricity charge or temperature",
                    new UnityAction(ToggleMode)
                )
            );

            physicalBehaviour.ContextMenuOptions.Buttons.Add(
                new ContextMenuButton(
                    "editValueDisplaySuffix",
                    "Edit unit suffix",
                    "Change the text shown after the number, e.g. W or \u00b0C",
                    new UnityAction(OpenEditSuffixDialog)
                )
            );

            contextMenuRegistered = true;
        }

        private void ToggleMode()
        {
            Mode = (Mode == ValueDisplayMode.Electricity) ? ValueDisplayMode.Heat : ValueDisplayMode.Electricity;
        }

        private void OpenEditSuffixDialog()
        {
            string current = (Mode == ValueDisplayMode.Electricity) ? ElectricitySuffix : HeatSuffix;
            Utils.OpenTextInputDialog(
                current,
                this,
                SetSuffix,
                "What suffix should be shown after the number?",
                "e.g. W or \u00b0C"
            );
        }

        private static void SetSuffix(ValueDisplayBehaviour behaviour, string value)
        {
            if (behaviour == null)
                return;

            if (behaviour.Mode == ValueDisplayMode.Electricity)
                behaviour.ElectricitySuffix = value;
            else
                behaviour.HeatSuffix = value;
        }

        private void Update()
        {
            if (physicalBehaviour == null || displayBehaviour == null)
                return;

            float value;
            string suffix;

            if (Mode == ValueDisplayMode.Electricity)
            {
                value = physicalBehaviour.Charge;
                suffix = ElectricitySuffix;
            }
            else
            {
                value = physicalBehaviour.Temperature;
                suffix = HeatSuffix;
            }

            string text = Mathf.RoundToInt(value) + suffix;
            if (text != lastRenderedText)
            {
                displayBehaviour.Value = text;
                displayBehaviour.UpdateDisplay();
                lastRenderedText = text;
            }
        }
    }

    public class ThresholdRule
    {
        public float Threshold;
        public string Text;
    }

    public class IfDisplayBehaviour : MonoBehaviour
    {
        public ValueDisplayMode Mode = ValueDisplayMode.Electricity;
        public List<ThresholdRule> Rules = new List<ThresholdRule>();

        [SkipSerialisation]
        private PhysicalBehaviour physicalBehaviour;

        [SkipSerialisation]
        private DisplayBehaviour displayBehaviour;

        [SkipSerialisation]
        private bool contextMenuRegistered;

        [SkipSerialisation]
        private string lastRenderedText;

        private void Awake()
        {
            physicalBehaviour = GetComponent<PhysicalBehaviour>();
            displayBehaviour = GetComponent<DisplayBehaviour>();
        }

        private void Start()
        {
            RegisterContextMenu();
        }

        private void RegisterContextMenu()
        {
            if (contextMenuRegistered)
                return;

            if (physicalBehaviour == null)
                physicalBehaviour = GetComponent<PhysicalBehaviour>();

            if (physicalBehaviour == null || physicalBehaviour.ContextMenuOptions == null)
                return;

            physicalBehaviour.ContextMenuOptions.Buttons.Add(
                new ContextMenuButton(
                    "toggleIfDisplayMode",
                    "Toggle mode (Electricity/Heat)",
                    "Switch what value this display watches: electricity charge or temperature",
                    new UnityAction(ToggleMode)
                )
            );

            physicalBehaviour.ContextMenuOptions.Buttons.Add(
                new ContextMenuButton(
                    "editIfDisplayRules",
                    "Edit rules",
                    "Set threshold:text pairs separated by commas, e.g. 0:Cold,50:Warm,100:Hot",
                    new UnityAction(OpenEditRulesDialog)
                )
            );

            contextMenuRegistered = true;
        }

        private void ToggleMode()
        {
            Mode = (Mode == ValueDisplayMode.Electricity) ? ValueDisplayMode.Heat : ValueDisplayMode.Electricity;
        }

        private void OpenEditRulesDialog()
        {
            Utils.OpenTextInputDialog(
                SerialiseRules(),
                this,
                SetRulesFromString,
                "Set rules as threshold:text, separated by commas",
                "0:Cold,50:Warm,100:Hot"
            );
        }

        private string SerialiseRules()
        {
            SortRules();
            var parts = new List<string>();
            foreach (var rule in Rules)
                parts.Add(rule.Threshold + ":" + rule.Text);
            return string.Join(",", parts.ToArray());
        }

        private static void SetRulesFromString(IfDisplayBehaviour behaviour, string value)
        {
            if (behaviour == null || value == null)
                return;

            var parsed = new List<ThresholdRule>();
            var entries = value.Split(',');
            foreach (var entry in entries)
            {
                var trimmed = entry.Trim();
                if (trimmed.Length == 0)
                    continue;

                var splitIndex = trimmed.IndexOf(':');
                if (splitIndex <= 0)
                    continue;

                var thresholdPart = trimmed.Substring(0, splitIndex).Trim();
                var textPart = trimmed.Substring(splitIndex + 1).Trim();

                float threshold;
                if (!float.TryParse(thresholdPart, out threshold))
                    continue;

                parsed.Add(new ThresholdRule { Threshold = threshold, Text = textPart });
            }

            if (parsed.Count > 0)
            {
                behaviour.Rules = parsed;
                behaviour.SortRules();
                behaviour.lastRenderedText = null;
            }
        }

        private void SortRules()
        {
            Rules.Sort(delegate (ThresholdRule a, ThresholdRule b)
            {
                return a.Threshold.CompareTo(b.Threshold);
            });
        }

        private string EvaluateText(float value)
        {
            if (Rules.Count == 0)
                return "";

            string result = Rules[0].Text;
            for (int i = 0; i < Rules.Count; i++)
            {
                if (Rules[i].Threshold <= value)
                    result = Rules[i].Text;
            }
            return result;
        }

        private void Update()
        {
            if (physicalBehaviour == null || displayBehaviour == null)
                return;

            float value = (Mode == ValueDisplayMode.Electricity)
                ? physicalBehaviour.Charge
                : physicalBehaviour.Temperature;

            string text = EvaluateText(value);
            if (text != lastRenderedText)
            {
                displayBehaviour.Value = text;
                displayBehaviour.UpdateDisplay();
                lastRenderedText = text;
            }
        }
    }

    public class TextSignalBehaviour : MonoBehaviour, Messages.IUse
    {
        public string DisplayText = "Hello World!";

        [SkipSerialisation]
        private PhysicalBehaviour physicalBehaviour;

        [SkipSerialisation]
        private bool contextMenuRegistered;

        private void Awake()
        {
            physicalBehaviour = GetComponent<PhysicalBehaviour>();
        }

        private void Start()
        {
            RegisterContextMenu();
        }

        private void RegisterContextMenu()
        {
            if (contextMenuRegistered)
                return;

            if (physicalBehaviour == null)
                physicalBehaviour = GetComponent<PhysicalBehaviour>();

            if (physicalBehaviour == null || physicalBehaviour.ContextMenuOptions == null)
                return;

            physicalBehaviour.ContextMenuOptions.Buttons.Add(
                new ContextMenuButton(
                    "changeSignalDisplayText",
                    "Edit display text",
                    "Change the text this signal writes to wired Text Displays",
                    new UnityAction(OpenEditDialog)
                )
            );

            contextMenuRegistered = true;
        }

        private void OpenEditDialog()
        {
            Utils.OpenTextInputDialog(
                DisplayText,
                this,
                SetDisplayText,
                "What should it say?",
                "Enter your text here"
            );
        }

        private static void SetDisplayText(TextSignalBehaviour behaviour, string value)
        {
            if (behaviour == null)
                return;

            behaviour.DisplayText = value;
        }

        public void Use(ActivationPropagation activation)
        {
            if (!enabled)
                return;

            PushTextToConnectedDisplays();
        }

        private void PushTextToConnectedDisplays()
        {
            foreach (var display in FindConnectedDisplays())
            {
                display.Value = DisplayText;
                display.UpdateDisplay();
            }
        }

        private IEnumerable<DisplayBehaviour> FindConnectedDisplays()
        {
            var seen = new HashSet<DisplayBehaviour>();

            if (physicalBehaviour != null && physicalBehaviour.ActivationNode != null)
            {
                var targets = physicalBehaviour.ActivationNode.Targets;
                if (targets != null)
                {
                    foreach (var target in targets)
                    {
                        if (target == null || target.Node == null)
                            continue;

                        var display = target.Node.GetComponent<DisplayBehaviour>();
                        if (display != null && seen.Add(display))
                            yield return display;
                    }
                }
            }

            foreach (var wire in GetComponents<WireBehaviour>())
            {
                if (wire == null)
                    continue;

                var other = wire.otherPhysicalBehaviour;
                if (other == null)
                    continue;

                var display = other.GetComponent<DisplayBehaviour>();
                if (display != null && seen.Add(display))
                    yield return display;
            }
        }
    }
}

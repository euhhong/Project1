using System;
using Project1.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 타이틀 화면 환경설정 창: 음량(전체·효과음·배경음 — AudioVolume, 바로 적용·저장) + Policy(팬게임·비영리 안내, 영어로 작게) + 닫기.
    /// </summary>
    public class TitleOptionsPanel
    {
        public const string PolicyText =
            "OLETUS is a non-commercial, fan-made game. It does not generate any revenue in any form.\n" +
            "Identity V and all related characters, names and assets belong to their respective owners. " +
            "This project is not affiliated with or endorsed by them.";

        private readonly TitleDialog dialog;
        private readonly Slider master, sfx, bgm;

        public bool IsOpen => dialog.IsOpen;
        public TitleDialog Dialog => dialog;

        public TitleOptionsPanel(VisualElement root, Font font)
        {
            dialog = new TitleDialog(root, font, 820f);
            var box = dialog.Box;

            TitleDialog.Text(box, "환경설정", font, 46, TitleDialog.Ink);
            Rule(box);

            Section(box, "음량", font);
            master = VolumeRow(box, "전체", font, AudioVolume.Master, AudioVolume.SetMaster);
            sfx = VolumeRow(box, "효과음", font, AudioVolume.Sfx, AudioVolume.SetSfx);
            bgm = VolumeRow(box, "배경음악", font, AudioVolume.Bgm, AudioVolume.SetBgm);

            var policyTitle = Section(box, "Policy", font);
            policyTitle.style.marginTop = 26f;
            var policy = TitleDialog.Text(box, PolicyText, font, 19, TitleDialog.InkDim);
            policy.style.marginTop = 4f;

            var row = TitleDialog.ButtonRow(box);
            TitleDialog.MakeButton(row, "닫기", font, Close, primary: true);
            dialog.DefaultAction = Close;
            dialog.CancelAction = Close;
        }

        public void Open()
        {
            // 저장된 값으로 맞춘다(값 글자도 같이 바뀐다)
            master.value = AudioVolume.Master * 100f;
            sfx.value = AudioVolume.Sfx * 100f;
            bgm.value = AudioVolume.Bgm * 100f;
            dialog.Open();
        }

        public void Close()
        {
            AudioVolume.Save();
            dialog.Close();
        }

        // ───────── 한 줄: 이름  [────●────]  값 ─────────

        private static Slider VolumeRow(VisualElement parent, string name, Font font, float value, Action<float> set)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 52f;
            parent.Add(row);

            var label = TitleDialog.Text(row, name, font, 30, TitleDialog.Ink);
            label.style.width = 150f;

            var slider = new Slider(0f, 100f) { value = value * 100f };
            slider.style.flexGrow = 1f;
            slider.style.marginLeft = slider.style.marginRight = 0f;
            row.Add(slider);
            var tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
                TitleDialog.SetBorder(tracker, 0f, Color.clear, 2f);
            }
            var dragger = slider.Q(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.backgroundColor = TitleDialog.CrimsonHover;
                TitleDialog.SetBorder(dragger, 1f, TitleDialog.Ink, 3f);
            }

            var valueLabel = TitleDialog.Text(row, Mathf.RoundToInt(value * 100f).ToString(), font, 28, TitleDialog.Ink);
            valueLabel.style.width = 70f;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            slider.RegisterValueChangedCallback(e =>
            {
                set(e.newValue / 100f);
                valueLabel.text = Mathf.RoundToInt(e.newValue).ToString();
            });
            return slider;
        }

        private static Label Section(VisualElement parent, string text, Font font)
        {
            var l = TitleDialog.Text(parent, text, font, 26, TitleDialog.CrimsonHover);
            l.style.marginBottom = 6f;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        private static void Rule(VisualElement parent)
        {
            var r = new VisualElement();
            r.style.height = 2f; r.style.width = 140f;
            r.style.backgroundColor = TitleDialog.Crimson;
            r.style.marginTop = 4f; r.style.marginBottom = 22f;
            parent.Add(r);
        }
    }
}

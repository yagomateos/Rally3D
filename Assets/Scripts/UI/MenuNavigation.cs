using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>
    /// Menu navigation with a controller: the left stick moves through the menus as well as the D-pad (also for pads
    /// the browser reports as a generic joystick), and each column of buttons wraps round, so going down from the
    /// last button lands on the first and up from the first lands on the last.
    /// </summary>
    public static class MenuNavigation
    {
        /// <summary>Adds stick and hat bindings to the UI module's Navigate action on top of Unity's defaults.</summary>
        public static void UseSticks(InputSystemUIInputModule module)
        {
            var asset = module.actionsAsset;
            var navigate = asset != null ? asset.FindAction("UI/Navigate") : null;
            if (navigate == null) return;
            bool wasEnabled = asset.enabled;
            asset.Disable();
            navigate.AddBinding("<Gamepad>/leftStick");
            navigate.AddBinding("<Joystick>/stick");
            navigate.AddBinding("<Joystick>/hat");
            if (wasEnabled) asset.Enable();
        }

        /// <summary>
        /// Gives the buttons under <paramref name="screen"/> that sit in one vertical column explicit up / down links
        /// that wrap round. Buttons in other columns (side by side) keep Unity's automatic navigation.
        /// </summary>
        public static void WrapColumn(Transform screen)
        {
            var buttons = screen.GetComponentsInChildren<Selectable>(true)
                .Where(b => b.transform is RectTransform)
                .ToList();
            if (buttons.Count < 2) return;

            // The column is the largest group of buttons sharing (roughly) the same horizontal centre.
            var column = buttons
                .GroupBy(b => Mathf.RoundToInt(Center(b).x / 40f))
                .OrderByDescending(g => g.Count())
                .First()
                .OrderByDescending(b => Center(b).y)
                .ToList();
            if (column.Count < 2) return;

            for (int i = 0; i < column.Count; i++)
            {
                var nav = column[i].navigation;
                var left = column[i].FindSelectableOnLeft();
                var right = column[i].FindSelectableOnRight();
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = column[(i - 1 + column.Count) % column.Count];
                nav.selectOnDown = column[(i + 1) % column.Count];
                nav.selectOnLeft = left;
                nav.selectOnRight = right;
                column[i].navigation = nav;
            }
        }

        private static Vector2 Center(Selectable s)
        {
            var rt = (RectTransform)s.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }
    }
}

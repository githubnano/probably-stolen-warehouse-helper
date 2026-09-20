using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;
using ColorPalette = Il2Cpp.RenderHandler.ColorPalette;

namespace WarehouseHelper
{
    /// <summary>
    /// Own native UI models, not clones of rendered GameObjects.
    /// ItemContextHandler.OnEventInit uses the same three native UI types.
    /// Each page owns all of its rows; no scene-owned row cache survives reload.
    /// </summary>
    public static class NativeMenu
    {
        private static PixelWindow _window;
        private static GridPixelElement _grid;
        private static readonly List<RichTextElement> _rows = new();
        private static List<string> _labels;
        private static Action<int> _onPick;
        private static Action _onCancel;
        private static ColorPalette _normal, _hoverColor;
        private static bool _open;
        private static int _shownFrame, _blockedFrame = -1, _pressedRow = -1, _hoverRow = -1;

        public static bool IsOpen => _open;
        public static bool BlocksGameInput => _open || Time.frameCount <= _blockedFrame;

        public static bool Show(List<string> labels, Action<int> onPick, Action onCancel = null)
        {
            DisposeView();
            if (labels == null || labels.Count == 0) return false;
            try
            {
                var context = ItemContextHandler.current;
                if (context == null || context.contextWindow?.rectTransform == null) return false;
                _normal = context.textColor;
                _hoverColor = context.textSelectColor;
                _labels = new List<string>(labels);
                _window = new PixelWindow(100, 100, false, 2, 2, null);
                _window.forceRaycast = true;
                _window.handler.SetParentOverlayRoot();
                _window.handler.gameObject.name = "WHBindMenu";
                _grid = new GridPixelElement(1, labels.Count, false);
                int width = 0;
                foreach (string label in labels)
                {
                    var row = new RichTextElement(false, -1, -1, true);
                    _rows.Add(row);
                    _grid.Attach(row.Cast<PixelElement>());
                    row.SetText(label, 10000, _normal);
                    width = Math.Max(width, row.widthPixels);
                }
                foreach (var row in _rows)
                {
                    row.SetFixedSize(width, -1);
                    row.Validate();
                }
                _grid.Validate();
                _window.Attach(_grid.Cast<PixelElement>());
                _window.Validate();
                _window.Show((Vector2)Input.mousePosition, context.contextTranslation, true);
                if (_window.bottomResizeButton != null)
                    _window.bottomResizeButton.gameObject.SetActive(false);
                _window.rectTransform.SetAsLastSibling();
                _onPick = onPick;
                _onCancel = onCancel;
                _shownFrame = Time.frameCount;
                _pressedRow = _hoverRow = -1;
                _open = true;

                return true;
            }
            catch (Exception e)
            {
                WarehouseHelperMod.Err("创建绑定菜单: " + e);
                DisposeView();
                return false;
            }
        }

        public static void Tick()
        {
            if (!_open) return;
            try
            {
                if (_window == null || _window.rectTransform == null || !_window.IsVisible())
                { Close(); return; }
                if (Time.frameCount <= _shownFrame) return;
                if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
                int row = HitRow();
                if (row != _hoverRow)
                {
                    if (_hoverRow >= 0) _rows[_hoverRow].SetText(_labels[_hoverRow], 10000, _normal);
                    if (row >= 0) _rows[row].SetText(_labels[row], 10000, _hoverColor);
                    _hoverRow = row;
                }
                if (Input.GetMouseButtonDown(1)) { Close(); return; }
                if (Input.GetMouseButtonDown(0))
                {
                    if (!Contains(_window.background.rectTransform)) { Close(); return; }
                    _pressedRow = row;
                }
                if (Input.GetMouseButtonUp(0))
                {
                    int pressed = _pressedRow;
                    _pressedRow = -1;
                    if (pressed >= 0 && row == pressed)
                    {
                        var callback = _onPick;
                        DisposeView();
                        _blockedFrame = Time.frameCount;
                        // A second page requires its own new click, not this release.
                        callback?.Invoke(row);
                    }
                }
            }
            catch (Exception e)
            {
                WarehouseHelperMod.Warn("绑定菜单已重置: " + e.Message);
                Close();
            }
        }

        private static int HitRow()
        {
            for (int i = 0; i < _rows.Count; i++)
                if (Contains(_rows[i].background.rectTransform)) return i;
            return -1;
        }

        private static bool Contains(RectTransform rect)
        {
            if (rect == null) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
        }

        public static void Close()
        {
            var callback = _onCancel;
            DisposeView();
            _blockedFrame = Time.frameCount;
            callback?.Invoke();
        }

        public static void Reset()
        {
            DisposeView();
            _blockedFrame = -1;
        }

        private static void DisposeView()
        {
            _open = false;
            _onPick = null;
            _onCancel = null;
            // Always clear managed references, even when Unity's destroyed object compares == null.
            var window = _window;
            var grid = _grid;
            _window = null;
            _grid = null;
            _labels = null;
            foreach (var row in _rows)
                try { if (row?.handler != null) UnityEngine.Object.Destroy(row.handler.gameObject); } catch { }
            _rows.Clear();
            _pressedRow = _hoverRow = -1;
            try { if (grid?.handler != null) UnityEngine.Object.Destroy(grid.handler.gameObject); } catch { }
            try
            {
                if (window?.rectTransform != null)
                {
                    window.Hide();
                    UnityEngine.Object.Destroy(window.handler.gameObject);
                }
            }
            catch { }
        }
    }
}

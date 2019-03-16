using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using Object = UnityEngine.Object;

namespace CanvasChecker
{
    /// <summary>
    /// Canvas用のTreeView
    /// </summary>
    internal class CanvasTreeView : TreeView
    {
        public class CanvasTreeViewItem : TreeViewItem
        {
            public Object unityObject;
            public Type objectType;
            public bool hasUnityObject;

            public CanvasTreeViewItem(string name, string valueString, Object unityObject = null, Type objectType = null)
            {
                if (string.IsNullOrEmpty(valueString))
                {
                    displayName = name;
                }
                else
                {
                    displayName = $"{name} = {valueString}";
                }

                this.unityObject = unityObject;
                this.hasUnityObject = objectType != null;
                this.objectType = objectType;
            }
        }

        private enum CanvasColumnType
        {
            Name,
            Object,
            Note,
        }

        /// <summary>
        /// Canvasの集合
        /// </summary>
        private OrderedCanvasSet _set;

        /// <summary>
        /// ツリー項目のためのID用のカウンタ
        /// </summary>
        private int _idCount;

        /// <summary>
        /// GetComponent用のリストキャッシュ
        /// </summary>
        private static List<Canvas> _getComponentsResult;

        public CanvasTreeView(TreeViewState treeViewState, MultiColumnHeader header) : base(treeViewState, header)
        {
        }

        /// <summary>
        /// ヘッダーを生成する
        /// </summary>
        public static MultiColumnHeaderState CreateHeaderState()
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Name"),
                    canSort = false,
                    autoResize = true,
                    minWidth = 200,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Object"),
                    canSort = false,
                    autoResize = true,
                    minWidth = 200,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Note"),
                    canSort = false,
                    autoResize = true,
                    minWidth = 120,
                },
            };

            var state = new MultiColumnHeaderState(columns);
            return state;
        }

        /// <summary>
        /// Canvasの集合を指定してセットアップする
        /// </summary>
        public void Setup(OrderedCanvasSet set)
        {
            _set = set;
            Reload();
            ExpandAll();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem {id = 0, depth = -1, displayName = "Root"};

            _idCount = 1;
            // カメラ系
            var cameraRoot = CreateItem("Camera");
            root.AddChild(cameraRoot);
            foreach (var cameraCanvasSet in _set.cameraCanvases)
            {
                // カメラ
                var cameraItem = CreateItem("Depth", cameraCanvasSet.CameraDepth.ToString(), cameraCanvasSet.camera);
                cameraRoot.AddChild(cameraItem);

                if (cameraCanvasSet.layerCanvases.Length > 0)
                {
                    foreach (var layerCanvasSet in cameraCanvasSet.layerCanvases)
                    {
                        // Sorting Layer
                        var valueString = $"{layerCanvasSet.sortingLayerName} ({layerCanvasSet.sortingLayerDepth})";
                        var layerItem = CreateItem("Sorting Layer", valueString);
                        cameraItem.AddChild(layerItem);
                        foreach (var canvas in layerCanvasSet.canvases)
                        {
                            // Canvas(Order in Layer)
                            AddCanvas(layerItem, canvas, "Order in Layer");
                        }
                    }
                }
            }

            // Overlay
            var overlayRoot = CreateItem("Overlay");
            root.AddChild(overlayRoot);
            EditorGUI.indentLevel++;
            foreach (var canvas in _set.overlayCanvases)
            {
                // Canvas(Sort Order)
                AddCanvas(overlayRoot, canvas, "Sort Order");
            }

            // depthを設定しなおす
            SetupDepthsFromParentsAndChildren(root);

            return root;
        }

        /// <summary>
        /// TreeViewの項目を作る
        /// </summary>
        private CanvasTreeViewItem CreateItem(string name, string valueString = null)
        {
            var item = new CanvasTreeViewItem(name, valueString) {id = _idCount++};
            return item;
        }


        /// <summary>
        /// TreeViewの項目を作る（Object指定）
        /// </summary>
        private CanvasTreeViewItem CreateItem<T>(string name, string valueString, T unityObject)
            where T : Object
        {
            var item = new CanvasTreeViewItem(name, valueString, unityObject, typeof(T)) {id = _idCount++};
            return item;
        }

        /// <summary>
        /// Canvas用のTreeViewの項目を追加する
        /// </summary>
        private void AddCanvas(TreeViewItem parent, Canvas canvas, string sortingOrderLabel)
        {
            var sortingOrderText = canvas != null ? canvas.sortingOrder.ToString() : "";
            var item = CreateItem(sortingOrderLabel, sortingOrderText, canvas);
            parent.AddChild(item);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as CanvasTreeViewItem;

            for (int i = 0; i < args.GetNumVisibleColumns(); i++)
            {
                CellGUI(args.GetCellRect(i), item, (CanvasColumnType) args.GetColumn(i), args);
            }
        }

        /// <summary>
        /// 各セルのGUIを描画する
        /// </summary>
        private void CellGUI(Rect rect, CanvasTreeViewItem item, CanvasColumnType columnType, RowGUIArgs args)
        {
            switch (columnType)
            {
                case CanvasColumnType.Name:
                    args.rowRect = rect;
                    base.RowGUI(args);
                    break;
                case CanvasColumnType.Note:
                    var canvas = item.unityObject as Canvas;
                    if (canvas != null)
                    {
                        if (IsInactiveSubCanvas(canvas))
                        {
                            var redTextStyle = new GUIStyle(EditorStyles.label)
                            {
                                normal = new GUIStyleState
                                {
                                    textColor = Color.red,
                                }
                            };
                            EditorGUI.LabelField(rect, new GUIContent("Inactive Sub-canvas"), redTextStyle);
                        }
                        else if (canvas.gameObject.activeInHierarchy == false)
                        {
                            EditorGUI.LabelField(rect, new GUIContent("Inactive Canvas"));
                        }
                    }

                    break;
                case CanvasColumnType.Object:
                    if (item.hasUnityObject)
                    {
                        var obj = item.unityObject;
                        var objectContent = EditorGUIUtility.ObjectContent(obj, item.objectType);
                        var objectStyle = new GUIStyle(EditorStyles.textField)
                        {
                            imagePosition = obj ? ImagePosition.ImageLeft : ImagePosition.TextOnly
                        };
                        var originalSize = EditorGUIUtility.GetIconSize();
                        EditorGUIUtility.SetIconSize(new Vector2(10, 10));
                        if (GUI.Button(rect, objectContent, objectStyle) && obj)
                        {
                            EditorGUIUtility.PingObject(obj);
                            Selection.activeObject = obj;
                        }

                        EditorGUIUtility.SetIconSize(originalSize);
                    }

                    break;
            }
        }

        /// <summary>
        /// 非アクティブなSub-canvasかどうか
        /// </summary>
        private static bool IsInactiveSubCanvas(Canvas canvas)
        {
            if (canvas == null || canvas.gameObject.activeInHierarchy)
            {
                return false;
            }

            var parent = canvas.transform.parent;
            if (parent == null)
            {
                return false;
            }

            if (_getComponentsResult == null)
            {
                _getComponentsResult = new List<Canvas>();
            }

            parent.GetComponentsInParent<Canvas>(true, _getComponentsResult);
            return _getComponentsResult.Count > 0;
        }
    }
}

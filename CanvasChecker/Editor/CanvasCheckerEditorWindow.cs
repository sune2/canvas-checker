using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine.SceneManagement;

namespace CanvasChecker
{
    /// <summary>
    /// シーン上のCanvasを描画順に列挙するためのWindow
    /// </summary>
    public class CanvasCheckerEditorWindow : EditorWindow
    {
        /// <summary>
        /// 現在のスクロール位置
        /// </summary>
        private Vector2 _scrollPosition;

        /// <summary>
        /// 描画順で整理されたCanvasのセット
        /// </summary>
        [SerializeField]
        private OrderedCanvasSet _canvasSet;

        /// <summary>
        /// Canvas用のTreeView
        /// </summary>
        private CanvasTreeView _treeView;

        /// <summary>
        /// TreeViewの状態
        /// </summary>
        [SerializeField]
        private TreeViewState _treeViewState;

        /// <summary>
        /// ヘッダーの状態
        /// </summary>
        [SerializeField]
        private MultiColumnHeaderState _headerState;

        /// <summary>
        /// ウィンドウの初期化
        /// </summary>
        [MenuItem("Tools/CanvasChecker")]
        public static void Open()
        {
            GetWindow<CanvasCheckerEditorWindow>("CanvasChecker");
        }

        private void OnEnable()
        {
            // ヘッダー初期化（状態があればそれを利用）
            var isFirstInit = _headerState == null;

            var headerState = CanvasTreeView.CreateHeaderState();
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(_headerState, headerState))
            {
                MultiColumnHeaderState.OverwriteSerializedFields(_headerState, headerState);
            }

            _headerState = headerState;
            var header = new MultiColumnHeader(_headerState);

            if (isFirstInit)
            {
                header.ResizeToFit();
            }

            // Stateは生成されていたらそれを使う
            if (_treeViewState == null)
            {
                _treeViewState = new TreeViewState();
            }

            _treeView = new CanvasTreeView(_treeViewState, header);
            if (_canvasSet != null)
            {
                _treeView.Setup(_canvasSet);
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(6);

            if (GUILayout.Button("Canvasを検索", GUILayout.Width(120f), GUILayout.Height(20f)))
            {
                var canvases = SearchAllCanvases();
                _canvasSet = new OrderedCanvasSet(canvases);
                _treeView.Setup(_canvasSet);
            }

            GUILayout.Space(4);

            if (_canvasSet == null)
            {
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            var rect = EditorGUILayout.GetControlRect(false, position.height - 40);
            _treeView.OnGUI(rect);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// シーン上のCanvasを全て検索する
        /// </summary>
        private static List<Canvas> SearchAllCanvases()
        {
            var canvases = new List<Canvas>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                canvases.AddRange(
                    scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                );
            }

            if (Application.isPlaying)
            {
                // DontDestroy対応
                var go = new GameObject("TempDontDestroyObject");
                DontDestroyOnLoad(go);

                canvases.AddRange(go.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                    .ToArray());

                DestroyImmediate(go);
            }

            return canvases;
        }
    }
}

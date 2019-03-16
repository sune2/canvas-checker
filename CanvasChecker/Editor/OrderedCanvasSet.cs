using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditorInternal;

namespace CanvasChecker
{
    /// <summary>
    /// 描画順に整理されたCanvasのセット
    /// </summary>
    [Serializable]
    internal class OrderedCanvasSet
    {
        /// <summary>
        /// カメラごとに集約されたCanvasのセット
        /// </summary>
        [Serializable]
        public class CanvasCameraSet
        {
            /// <summary>
            /// Sorting Layerごとに集約されたCanvasのセット
            /// </summary>
            [Serializable]
            public class CanvasSortingLayerSet
            {
                /// <summary>
                /// Sorting LayerのDepth
                /// </summary>
                public int sortingLayerDepth;

                /// <summary>
                /// Sorting Layerの名前
                /// </summary>
                public string sortingLayerName;

                /// <summary>
                /// Canvasの配列
                /// </summary>
                public Canvas[] canvases;

                public CanvasSortingLayerSet(int sortingLayerDepth, Canvas[] canvases)
                {
                    this.sortingLayerDepth = sortingLayerDepth;
                    sortingLayerName = canvases.First().sortingLayerName;
                    this.canvases = canvases.OrderBy(x => x.sortingOrder).ToArray();
                }
            }

            /// <summary>
            /// カメラ
            /// </summary>
            public Camera camera;

            /// <summary>
            /// カメラのDepth
            /// カメラがnullなら0
            /// </summary>
            public float CameraDepth => camera != null ? camera.depth : 0f;

            /// <summary>
            /// Canvas
            /// </summary>
            public CanvasSortingLayerSet[] layerCanvases;

            public CanvasCameraSet(Camera camera, Canvas[] canvases)
            {
                this.camera = camera;
                layerCanvases = canvases.ToLookup(x => GetSortingLayerDepth(x.sortingLayerID))
                    .Select(group => new CanvasSortingLayerSet(group.Key, group.ToArray()))
                    .OrderBy(x => x.sortingLayerDepth)
                    .ToArray();
            }
        }

        /// <summary>
        /// OverlayのCanvasの配列
        /// </summary>
        public Canvas[] overlayCanvases;

        /// <summary>
        /// カメラごとに集約されたCanvasのセットの配列
        /// </summary>
        public CanvasCameraSet[] cameraCanvases;

        public OrderedCanvasSet(IList<Canvas> canvases)
        {
            overlayCanvases = canvases.Where(x => x.renderMode == RenderMode.ScreenSpaceOverlay)
                .OrderBy(x => x.sortingOrder)
                .ToArray();
            cameraCanvases = canvases.Where(x => x.renderMode != RenderMode.ScreenSpaceOverlay)
                .ToLookup(x => x.worldCamera)
                .Select(group => new CanvasCameraSet(group.Key, group.ToArray()))
                .OrderBy(x => x.CameraDepth)
                .ToArray();
        }

        private static Dictionary<int, int> _sortingLayerDepth;

        /// <summary>
        /// Sorting LayerのIDからdepthを取得する
        /// </summary>
        private static int GetSortingLayerDepth(int sortingLayerId)
        {
            if (_sortingLayerDepth == null)
            {
                _sortingLayerDepth = new Dictionary<int, int>();
            }

            int depth;
            if (_sortingLayerDepth.TryGetValue(sortingLayerId, out depth) == false)
            {
                UpdateSortingLayer();
                _sortingLayerDepth.TryGetValue(sortingLayerId, out depth);
            }

            return depth;
        }

        /// <summary>
        /// Sorting Layerのdepthの対応を更新する
        /// </summary>
        private static void UpdateSortingLayer()
        {
            var sortingLayerUniqueIDsProperty = typeof(InternalEditorUtility)
                .GetProperty("sortingLayerUniqueIDs", BindingFlags.Static | BindingFlags.NonPublic);
            var layerIds = (int[]) sortingLayerUniqueIDsProperty.GetValue(null, new object[0]);
            _sortingLayerDepth = new Dictionary<int, int>();
            for (var i = 0; i < layerIds.Length; i++)
            {
                var layerId = layerIds[i];
                _sortingLayerDepth[layerId] = i;
            }
        }
    }
}

﻿using UnityEngine;
using System.Collections.Generic;
using WaterBuoyancy.Collections;

namespace WaterBuoyancy
{
    [RequireComponent(typeof(BoxCollider))]
    public class WaterVolume : MonoBehaviour
    {
        public const string TAG = "Water Volume";

        [SerializeField]
        private float density = 1f;

        [SerializeField]
        private Transform debugTrans; // Only for debugging

        private Mesh mesh;
        private Vector3[] meshLocalVertices;
        private Vector3[] meshWorldVertices;
        private List<Vector3[]> meshTrianglesInWorldSpace;
        private Vector3[] lastCachedTrianglePolygon;

        public float Density
        {
            get { return this.density; }
        }

        protected virtual void Awake()
        {
            this.mesh = this.GetComponent<MeshFilter>().mesh;
            this.CacheMeshVertices();
            this.CacheMeshTrianglesInWorldSpace();
        }

        protected virtual void Update()
        {
            this.CacheMeshVertices();
            this.CacheMeshTrianglesInWorldSpace();
        }

        protected virtual void OnDrawGizmos()
        {
            if (this.meshTrianglesInWorldSpace == null)
            {
                return;
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(debugTrans.position, 0.1f);

            for (int i = 0; i < this.meshTrianglesInWorldSpace.Count; i++)
            {
                if (MathfUtils.IsPointInTriangle(debugTrans.position, this.meshTrianglesInWorldSpace[i], false, true, false))
                {
                    Gizmos.color = Color.green;

                    Gizmos.DrawLine(this.meshTrianglesInWorldSpace[i][0], this.meshTrianglesInWorldSpace[i][1]);
                    Gizmos.DrawLine(this.meshTrianglesInWorldSpace[i][1], this.meshTrianglesInWorldSpace[i][2]);
                    Gizmos.DrawLine(this.meshTrianglesInWorldSpace[i][2], this.meshTrianglesInWorldSpace[i][0]);
                }
            }
        }

        public Vector3[] GetSurroundingTrianglePolygon(Vector3 point)
        {
            if (this.lastCachedTrianglePolygon != null &&
                MathfUtils.IsPointInTriangle(point, this.lastCachedTrianglePolygon, false, true, false))
            {
                return this.lastCachedTrianglePolygon;
            }

            for (int i = 0; i < this.meshTrianglesInWorldSpace.Count; i++)
            {
                if (MathfUtils.IsPointInTriangle(point, this.meshTrianglesInWorldSpace[i], false, true, false))
                {
                    this.lastCachedTrianglePolygon = this.meshTrianglesInWorldSpace[i];
                    return this.lastCachedTrianglePolygon;
                }
            }

            throw new System.ArgumentException("Point not in the water", "point");
        }

        public Vector3[] GetClosestPointsOnWaterSurface(Vector3 point, int pointsCount)
        {
            MinHeap<Vector3> allPoints = new MinHeap<Vector3>(new Vector3HorizontalDistanceComparer(point));
            for (int i = 0; i < this.meshWorldVertices.Length; i++)
            {
                allPoints.Add(this.meshWorldVertices[i]);
            }

            Vector3[] closest = new Vector3[pointsCount];
            for (int i = 0; i < closest.Length; i++)
            {
                closest[i] = allPoints.Remove();
            }

            return closest;
        }

        private bool IsPointUnderWater(Vector3 point)
        {
            return this.GetWaterLevel(point) - point.y > 0f;
        }

        public float GetWaterLevel(Vector3 point)
        {
            Vector3[] meshPolygon = this.GetSurroundingTrianglePolygon(point);

            Vector3 planeV1 = meshPolygon[1] - meshPolygon[0];
            Vector3 planeV2 = meshPolygon[2] - meshPolygon[0];
            Vector3 planeNormal = Vector3.Cross(planeV1, planeV2).normalized;
            if (planeNormal.y < 0f)
            {
                planeNormal *= -1f;
            }

            float yOnWaterSurface = (-(point.x * planeNormal.x) - (point.z * planeNormal.z) + Vector3.Dot(meshPolygon[0], planeNormal)) / planeNormal.y;
            //Vector3 pointOnWaterSurface = new Vector3(point.x, yOnWaterSurface, point.z);
            //DebugUtils.DrawPoint(pointOnWaterSurface, Color.magenta);
            //Debug.DrawLine(pointOnWaterSurface, pointOnWaterSurface + planeNormal, Color.blue);
            //Debug.DrawLine(pointOnWaterSurface, meshPoligon[0], Color.green);
            //Debug.DrawLine(pointOnWaterSurface, meshPoligon[1], Color.green);
            //Debug.DrawLine(pointOnWaterSurface, meshPoligon[2], Color.green);

            return yOnWaterSurface;
        }

        private void CacheMeshVertices()
        {
            this.meshLocalVertices = this.mesh.vertices;
            this.meshWorldVertices = this.ConvertPointsToWorldSpace(meshLocalVertices);
        }

        private void CacheMeshTrianglesInWorldSpace()
        {
            int[] triangles = this.mesh.triangles;
            if (this.meshTrianglesInWorldSpace == null)
            {
                this.meshTrianglesInWorldSpace = new List<Vector3[]>(triangles.Length / 3);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    this.meshTrianglesInWorldSpace.Add(new Vector3[3]);
                }
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                this.meshTrianglesInWorldSpace[i / 3][0] = this.meshWorldVertices[triangles[i]];
                this.meshTrianglesInWorldSpace[i / 3][1] = this.meshWorldVertices[triangles[i + 1]];
                this.meshTrianglesInWorldSpace[i / 3][2] = this.meshWorldVertices[triangles[i + 2]];
            }
        }

        private Vector3[] ConvertPointsToWorldSpace(Vector3[] points)
        {
            Vector3[] worldPoints = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                worldPoints[i] = this.transform.TransformPoint(points[i]);
            }

            return worldPoints;
        }

        private class Vector3HorizontalDistanceComparer : IComparer<Vector3>
        {
            private Vector3 distanceToVector;

            public Vector3HorizontalDistanceComparer(Vector3 distanceTo)
            {
                this.distanceToVector = distanceTo;
            }

            public int Compare(Vector3 v1, Vector3 v2)
            {
                v1.y = 0;
                v2.y = 0;
                float v1Distance = (v1 - distanceToVector).sqrMagnitude;
                float v2Distance = (v2 - distanceToVector).sqrMagnitude;

                if (v1Distance < v2Distance)
                {
                    return -1;
                }
                else if (v1Distance > v2Distance)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}

using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Rendering;

namespace Ashsvp
{
	public class Skidmarks : MonoBehaviour
	{
		public Material skidmarksMaterial;
		[HideInInspector]
		public float SkidmarkWidth = 0.5f;

		private const int DesktopMaxSkidMarks = 2048;
		private const int MobileMaxSkidMarks = 512;
		private const float DesktopRetentionSeconds = 90f;
		private const float MobileRetentionSeconds = 30f;
		private const float RetentionCheckSeconds = 5f;
		private const float contact_Offset = 0.02f;
		private const float MinDistance = 0.25f;
		private const float MinDistanceSquare = MinDistance * MinDistance;
		private const float MaxOpacity = 1.0f;

		class SkidMarkSection
		{
			public Vector3 Pos = Vector3.zero;
			public Vector3 Normal = Vector3.zero;
			public Vector4 Tangent = Vector4.zero;
			public Vector3 Posl = Vector3.zero;
			public Vector3 Posr = Vector3.zero;
			public Color32 Colour;
			public int LastIndex;
			public int Generation;
		};

		int markIndex;
		int maxSkidMarks;
		int generation = 1;
		SkidMarkSection[] skidmarks;
		Mesh marksMesh;
		MeshRenderer mr;
		MeshFilter mf;

		Vector3[] vertices;
		Vector3[] normals;
		Vector4[] tangents;
		Color32[] colors;
		Vector2[] uvs;
		int[] triangles;

		bool meshUpdated;
		bool hasVisibleMarks;
		bool initialized;
		float lastMarkTime;
		Coroutine retentionRoutine;

		Color32 black = Color.black;


		protected void Awake()
		{
			if (transform.position != Vector3.zero)
			{
				transform.position = Vector3.zero;
				transform.rotation = Quaternion.identity;
			}
		}

		protected void Start()
		{
			// Parked traffic does not allocate a dynamic mesh or large vertex arrays.
			// AddSkidMark initializes the bounded pool on first real tire slip.
			enabled = false;
		}

		void EnsureInitialized()
		{
			if (initialized) return;
			initialized = true;
			maxSkidMarks = Application.isMobilePlatform
				? MobileMaxSkidMarks
				: DesktopMaxSkidMarks;
			skidmarks = new SkidMarkSection[maxSkidMarks];

			for (int i = 0; i < maxSkidMarks; i++)
			{
				skidmarks[i] = new SkidMarkSection();
			}

			mf = GetComponent<MeshFilter>();
			mr = GetComponent<MeshRenderer>();

			if (mr == null)
			{
				mr = gameObject.AddComponent<MeshRenderer>();
			}

			marksMesh = new Mesh();
			marksMesh.MarkDynamic();

			if (mf == null)
			{
				mf = gameObject.AddComponent<MeshFilter>();
			}
			mf.sharedMesh = marksMesh;

			vertices = new Vector3[maxSkidMarks * 4];
			normals = new Vector3[maxSkidMarks * 4];
			tangents = new Vector4[maxSkidMarks * 4];
			colors = new Color32[maxSkidMarks * 4];
			uvs = new Vector2[maxSkidMarks * 4];
			triangles = new int[maxSkidMarks * 6];

			mr.shadowCastingMode = ShadowCastingMode.Off;
			mr.receiveShadows = false;
			mr.sharedMaterial = skidmarksMaterial;
			mr.lightProbeUsage = LightProbeUsage.Off;
			mr.enabled = false;

			// AddSkidMark re-enables this component only when a mesh upload is pending.
			// This removes an otherwise permanent LateUpdate callback per Car.
		}

		protected void LateUpdate()
		{
			if (!meshUpdated) return;
			meshUpdated = false;

			marksMesh.vertices = vertices;
			marksMesh.normals = normals;
			marksMesh.tangents = tangents;
			marksMesh.triangles = triangles;
			marksMesh.colors32 = colors;
			marksMesh.uv = uvs;

			UpdateMeshBounds();
			enabled = false;
		}

		public int AddSkidMark(Vector3 pos, Vector3 normal, float opacity, int lastIndex)
		{
			if (opacity > 1) opacity = 1.0f;
			else if (opacity < 0) return -1;

			black.a = (byte)(opacity * 255);
			return AddSkidMark(pos, normal, black, lastIndex);
		}

		public int AddSkidMark(Vector3 pos, Vector3 normal, Color32 colour, int lastIndex)
		{
			if (colour.a == 0) return -1;
			EnsureInitialized();
			if (lastIndex < 0 || lastIndex >= maxSkidMarks ||
				skidmarks[lastIndex].Generation != generation)
			{
				lastIndex = -1;
			}

			SkidMarkSection lastSection = null;
			Vector3 distAndDirection = Vector3.zero;
			Vector3 newPos = pos + normal * contact_Offset;
			if (lastIndex != -1)
			{
				lastSection = skidmarks[lastIndex];
				distAndDirection = newPos - lastSection.Pos;
				if (distAndDirection.sqrMagnitude < MinDistanceSquare)
				{
					return lastIndex;
				}

				if (distAndDirection.sqrMagnitude > MinDistanceSquare * 10)
				{
					lastIndex = -1;
					lastSection = null;
				}
			}

			colour.a = (byte)(colour.a * MaxOpacity);

			SkidMarkSection curSection = skidmarks[markIndex];

			curSection.Pos = newPos;
			curSection.Normal = normal;
			curSection.Colour = colour;
			curSection.LastIndex = lastIndex;
			curSection.Generation = generation;

			if (lastSection != null)
			{
				Vector3 xDirection = Vector3.Cross(distAndDirection, normal).normalized;
				curSection.Posl = curSection.Pos + xDirection * SkidmarkWidth * 0.5f;
				curSection.Posr = curSection.Pos - xDirection * SkidmarkWidth * 0.5f;
				curSection.Tangent = new Vector4(xDirection.x, xDirection.y, xDirection.z, 1);

				if (lastSection.LastIndex == -1)
				{
					lastSection.Tangent = curSection.Tangent;
					lastSection.Posl = curSection.Pos + xDirection * SkidmarkWidth * 0.5f;
					lastSection.Posr = curSection.Pos - xDirection * SkidmarkWidth * 0.5f;
				}
			}

			UpdateSkidmarksMesh();

			int curIndex = markIndex;
			markIndex = ++markIndex % maxSkidMarks;

			return curIndex;
		}

		void UpdateSkidmarksMesh()
		{
			SkidMarkSection curr = skidmarks[markIndex];

			if (curr.LastIndex == -1)
			{
				ClearSegment(markIndex);
				meshUpdated = true;
				enabled = true;
				return;
			}

			SkidMarkSection last = skidmarks[curr.LastIndex];
			vertices[markIndex * 4 + 0] = last.Posl;
			vertices[markIndex * 4 + 1] = last.Posr;
			vertices[markIndex * 4 + 2] = curr.Posl;
			vertices[markIndex * 4 + 3] = curr.Posr;

			normals[markIndex * 4 + 0] = last.Normal;
			normals[markIndex * 4 + 1] = last.Normal;
			normals[markIndex * 4 + 2] = curr.Normal;
			normals[markIndex * 4 + 3] = curr.Normal;

			tangents[markIndex * 4 + 0] = last.Tangent;
			tangents[markIndex * 4 + 1] = last.Tangent;
			tangents[markIndex * 4 + 2] = curr.Tangent;
			tangents[markIndex * 4 + 3] = curr.Tangent;

			colors[markIndex * 4 + 0] = last.Colour;
			colors[markIndex * 4 + 1] = last.Colour;
			colors[markIndex * 4 + 2] = curr.Colour;
			colors[markIndex * 4 + 3] = curr.Colour;

			uvs[markIndex * 4 + 0] = new Vector2(0, 0);
			uvs[markIndex * 4 + 1] = new Vector2(1, 0);
			uvs[markIndex * 4 + 2] = new Vector2(0, 1);
			uvs[markIndex * 4 + 3] = new Vector2(1, 1);

			triangles[markIndex * 6 + 0] = markIndex * 4 + 0;
			triangles[markIndex * 6 + 2] = markIndex * 4 + 1;
			triangles[markIndex * 6 + 1] = markIndex * 4 + 2;

			triangles[markIndex * 6 + 3] = markIndex * 4 + 2;
			triangles[markIndex * 6 + 5] = markIndex * 4 + 1;
			triangles[markIndex * 6 + 4] = markIndex * 4 + 3;

			meshUpdated = true;
			hasVisibleMarks = true;
			lastMarkTime = Time.unscaledTime;
			if (mr != null) mr.enabled = true;
			EnsureRetentionRoutine();
			enabled = true;
		}

		private void OnEnable()
		{
			EnsureRetentionRoutine();
		}

		void EnsureRetentionRoutine()
		{
			if (!hasVisibleMarks || retentionRoutine != null ||
				!gameObject.activeInHierarchy)
			{
				return;
			}

			retentionRoutine = StartCoroutine(ClearExpiredMarks());
		}

		private void OnDisable()
		{
			// Disabling this component after a one-frame mesh upload must not stop
			// retention. A pooled/hidden GameObject does stop Unity coroutines, so
			// release the handle and let OnEnable schedule it again later.
			if (!gameObject.activeInHierarchy)
			{
				retentionRoutine = null;
			}
		}

		IEnumerator ClearExpiredMarks()
		{
			WaitForSecondsRealtime wait =
				new WaitForSecondsRealtime(RetentionCheckSeconds);
			float retention = Application.isMobilePlatform
				? MobileRetentionSeconds
				: DesktopRetentionSeconds;

			while (gameObject.activeInHierarchy && hasVisibleMarks)
			{
				yield return wait;
				if (Time.unscaledTime - lastMarkTime < retention) continue;

				ClearAllMarks();
				retentionRoutine = null;
				yield break;
			}

			retentionRoutine = null;
		}

		void ClearAllMarks()
		{
			if (vertices != null) Array.Clear(vertices, 0, vertices.Length);
			if (normals != null) Array.Clear(normals, 0, normals.Length);
			if (tangents != null) Array.Clear(tangents, 0, tangents.Length);
			if (colors != null) Array.Clear(colors, 0, colors.Length);
			if (uvs != null) Array.Clear(uvs, 0, uvs.Length);
			if (triangles != null) Array.Clear(triangles, 0, triangles.Length);

			if (skidmarks != null)
			{
				for (int i = 0; i < skidmarks.Length; i++)
				{
					skidmarks[i].LastIndex = -1;
					skidmarks[i].Generation = 0;
					skidmarks[i].Colour = default;
				}
			}

			markIndex = 0;
			generation = generation == int.MaxValue ? 1 : generation + 1;
			meshUpdated = false;
			hasVisibleMarks = false;
			if (marksMesh != null) marksMesh.Clear(false);
			if (mr != null) mr.enabled = false;
		}

		public void ClearForOwnerDeactivation()
		{
			if (retentionRoutine != null)
			{
				StopCoroutine(retentionRoutine);
				retentionRoutine = null;
			}
			ClearAllMarks();
		}

		void ClearSegment(int segmentIndex)
		{
			int vertexStart = segmentIndex * 4;
			int triangleStart = segmentIndex * 6;

			for (int i = 0; i < 4; i++)
			{
				colors[vertexStart + i] = default;
			}

			for (int i = 0; i < 6; i++)
			{
				triangles[triangleStart + i] = 0;
			}
		}

		void UpdateMeshBounds()
		{
			bool hasPoint = false;
			Bounds bounds = default;

			for (int segment = 0; segment < maxSkidMarks; segment++)
			{
				int vertexStart = segment * 4;
				if (colors[vertexStart].a == 0) continue;

				for (int i = 0; i < 4; i++)
				{
					Vector3 point = vertices[vertexStart + i];
					if (!hasPoint)
					{
						bounds = new Bounds(point, Vector3.one * 0.05f);
						hasPoint = true;
					}
					else
					{
						bounds.Encapsulate(point);
					}
				}
			}

			marksMesh.bounds = hasPoint
				? bounds
				: new Bounds(Vector3.zero, Vector3.one * 0.05f);
		}

		private void OnDestroy()
		{
			retentionRoutine = null;
			if (marksMesh == null) return;

			if (mf != null && mf.sharedMesh == marksMesh)
			{
				mf.sharedMesh = null;
			}

			if (Application.isPlaying) Destroy(marksMesh);
			else DestroyImmediate(marksMesh);
			marksMesh = null;
		}
	}
}

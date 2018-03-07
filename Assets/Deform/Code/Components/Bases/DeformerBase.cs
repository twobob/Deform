﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Deform
{
	[ExecuteInEditMode]
	public abstract class DeformerBase : MonoBehaviour
	{
		[SerializeField, HideInInspector]
		protected MeshFilter target;
		[SerializeField, HideInInspector]
		protected SkinnedMeshRenderer skinnedTarget;
		[SerializeField, HideInInspector]
		protected VertexData[] vertexData;
		[SerializeField, HideInInspector]
		protected Mesh originalMesh;
		[SerializeField, HideInInspector]
		protected Mesh deformMesh;

		private List<Vector3> originalNormals = new List<Vector3> ();

		protected bool asyncUpdateInProgress { get; private set; }
		
		public int VertexCount { get { return originalMesh.vertexCount; } }
		public float SyncedTime { get; private set; }
		public float SyncedDeltaTime { get; private set; }
		public TransformData SyncedTransform { get; private set; }
		public Bounds Bounds { get; private set; }
		public MeshFilter Target { get { return target; } }

		private void OnDestroy ()
		{
			DiscardChanges ();
		}

		public void SetTarget (MeshFilter meshFilter, bool recreateVertexData = true)
		{
			// Assign the target.
			skinnedTarget = null;
			target = meshFilter;

			// If it's not null, the object was probably duplicated
			if (originalMesh == null)
				originalMesh = Instantiate (target.sharedMesh);
			else
				originalMesh = Instantiate (originalMesh);


			// Change the mesh to one we can modify.
			deformMesh = target.sharedMesh = Instantiate (originalMesh);

			// Cache the original bounds.
			Bounds = originalMesh.bounds;

			// Cache the original normals.
			deformMesh.GetNormals (originalNormals);

			deformMesh.name = transform.name + " Deform Mesh";
			originalMesh.name = "Original";

			// Create chunk data.
			if (recreateVertexData)
				RecreateVertexData ();
		}

		public void SetTarget (SkinnedMeshRenderer skinnedMesh, bool recreateVertexData = true)
		{
			target = null;
			// Assign the target.
			skinnedTarget = skinnedMesh;

			// If it's not null, the object was probably duplicated
			if (originalMesh == null)
				// Store the original mesh.
				originalMesh = Instantiate (skinnedTarget.sharedMesh);
			else
				originalMesh = Instantiate (originalMesh);

			// Change the mesh to one we can modify.
			deformMesh = skinnedTarget.sharedMesh = Instantiate (originalMesh);

			Bounds = originalMesh.bounds;
			// Cache the original normals.
			originalMesh.GetNormals (originalNormals);

			// Create chunk data.
			if (recreateVertexData)
				RecreateVertexData ();
		}

		public void UpdateMeshInstant (NormalsCalculationMode normalsCalculation, float smoothingAngle)
		{
			// Don't update if another update is in progress.
			if (asyncUpdateInProgress)
				return;

			DeformVertexData ();
			ApplyVertexDataToTarget (normalsCalculation, smoothingAngle);
			ResetVertexData ();
		}

		public async void UpdateMeshAsync (NormalsCalculationMode normalsCalculation, float smoothingAngle, Action onComplete = null)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				Debug.LogError ("UpdateMeshAsync doesn't work in edit-mode");
				return;
			}
#endif
			if (asyncUpdateInProgress)
				return;

			asyncUpdateInProgress = true;
			await new WaitForBackgroundThread ();
			DeformVertexData ();
			await new WaitForUpdate ();
			asyncUpdateInProgress = false;

			// We have to handle the scenario in which the update starts in play mode and finishes in edit mode.
			if (!Application.isPlaying)
				return;

			ApplyVertexDataToTarget (normalsCalculation, smoothingAngle);
			ResetVertexData ();

			if (onComplete != null)
				onComplete.Invoke ();
		}

		public void UpdateNormals (NormalsCalculationMode normalsCalculation, float smoothingAngle)
		{
			switch (normalsCalculation)
			{
				case NormalsCalculationMode.Unity:
					deformMesh.RecalculateNormals ();
					break;
				case NormalsCalculationMode.Smooth:
					deformMesh.RecalculateNormals (smoothingAngle);
					break;
				case NormalsCalculationMode.Maintain:
					break;
				case NormalsCalculationMode.Original:
					deformMesh.SetNormals (originalNormals);
					break;
			}
		}

		public void UpdateSyncedTime ()
		{
			SyncedDeltaTime = Time.time - SyncedTime;
			SyncedTime = Time.time;
		}

		public void UpdateTransformData ()
		{
			SyncedTransform = new TransformData (transform);
		}

		public void RecreateVertexData ()
		{
			vertexData = VertexDataUtil.GetVertexData (deformMesh);
		}

		protected void ApplyVertexDataToTarget (NormalsCalculationMode normalsCalculation, float smoothingAngle)
		{
			VertexDataUtil.ApplyVertexData (vertexData, deformMesh);
			UpdateNormals (normalsCalculation, smoothingAngle);

			deformMesh.RecalculateBounds ();
		}

		protected abstract void DeformVertexData ();

		protected void ResetVertexData ()
		{
			VertexDataUtil.ResetVertexData (vertexData);
		}

		public void DiscardChanges ()
		{
			if (originalMesh != null)
			{
				deformMesh = Instantiate (originalMesh);
			}
		}
	}
}
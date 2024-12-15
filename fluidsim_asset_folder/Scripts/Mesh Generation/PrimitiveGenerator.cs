using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrimitiveGenerator
	{

		public static MeshRenderer GeneratePlane(GameObject gameObject, int resolution, Vector2 dimensions)
		{
			MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
			MeshCollider collider = gameObject.AddComponent<MeshCollider>();
			MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
			Mesh mesh = new Mesh();
			meshFilter.sharedMesh = mesh;


			int resX = resolution; // 2 minimum
			int resZ = resolution;

			#region Vertices		
			Vector3[] vertices = new Vector3[resX * resZ];
			for (int z = 0; z < resZ; z++)
			{
				// [ -length / 2, length / 2 ]
				float zPos = ((float)z / (resZ - 1) - .5f) * dimensions.y;
				for (int x = 0; x < resX; x++)
				{
					// [ -width / 2, width / 2 ]
					float xPos = ((float)x / (resX - 1) - .5f) * dimensions.x;
					vertices[x + z * resX] = new Vector3(xPos, 0f, zPos);
				}
			}
			#endregion

			#region Normales
			Vector3[] normales = new Vector3[vertices.Length];
			for (int n = 0; n < normales.Length; n++)
				normales[n] = Vector3.up;
			#endregion

			#region UVs		
			Vector2[] uvs = new Vector2[vertices.Length];
			for (int v = 0; v < resZ; v++)
			{
				for (int u = 0; u < resX; u++)
				{
					uvs[u + v * resX] = new Vector2((float)u / (resX - 1), (float)v / (resZ - 1));
				}
			}
			#endregion

			#region Triangles
			int nbFaces = (resX - 1) * (resZ - 1);
			int[] triangles = new int[nbFaces * 6];
			int t = 0;
			for (int face = 0; face < nbFaces; face++)
			{
				// Retrieve lower left corner from face ind
				int i = face % (resX - 1) + (face / (resZ - 1) * resX);

				triangles[t++] = i + resX;
				triangles[t++] = i + 1;
				triangles[t++] = i;

				triangles[t++] = i + resX;
				triangles[t++] = i + resX + 1;
				triangles[t++] = i + 1;
			}
			#endregion
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

			mesh.vertices = vertices;
			mesh.normals = normales;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			mesh.RecalculateBounds();
			mesh.Optimize();
			mesh.UploadMeshData(true);

			collider.sharedMesh = mesh;

			return renderer;
		}

		public static Mesh GenerateTerrainMesh(int resX, int resZ, Vector2 dimension, Vector2 uvStart, Vector2 uvScale)
		{
			Mesh mesh = new Mesh();
			#region Vertices		
			Vector3[] vertices = new Vector3[(resX + 1) * (resZ + 1)];
			Vector2[] uvs = new Vector2[vertices.Length];

			Vector2 gridRcpSize = Vector2.one / (resX + 1);
			Vector2 halfGridRcpSize = (gridRcpSize) * 0.5f;

			int v = 0;
			for (int z = 0; z <= resZ; z++)
			{
				float zPos = (((float)(z) / resZ) - 0.5f) * dimension.y;
				for (int x = 0; x <= resX; x++)
				{
					Vector2 uv = new Vector2(x, z);
					float xPos = (((float)(x) / resX) - 0.5f) * dimension.x;
					//Vector2 uv = new Vector2((float)x / (resX), (float)z);
					uv = (uv * gridRcpSize) + halfGridRcpSize;//+ uvParams.xy;

					uv.Scale(uvScale);
					uvs[v] = uv + uvStart;

					vertices[v] = new Vector3(xPos, 0, zPos);
					v++;
				}
			}
			#endregion

			#region Triangles
			int nbFaces = (resX) * (resZ);
			int[] triangles = new int[nbFaces * 6];
			int t = 0;

			int vert = 0;
			for (int z = 0; z < resZ; z++)
			{
				for (int x = 0; x < resX; x++)
				{
					triangles[t++] = vert;
					triangles[t++] = vert + resX + 2;
					triangles[t++] = vert + 1;

					triangles[t++] = vert;
					triangles[t++] = vert + resX + 1;
					triangles[t++] = vert + resX + 2;

					vert++;
				}
				vert++;
			}
			#endregion
			//m_Mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.vertices = vertices;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			return mesh;
		}
	}
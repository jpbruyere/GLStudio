//
//  Rendering.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2017 jp
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using Tetra.DynamicShading;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Crow;

namespace GLStudio
{
	public class Rendering : IValueChange
	{
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged(string MemberName, object _value)
		{
			if (ValueChanged != null)				
				ValueChanged.Invoke(this, new ValueChangeEventArgs(MemberName, _value));
		}
		#endregion

		[StructLayout(LayoutKind.Sequential)]
		public struct UBOSharedData
		{
			public Vector4 Color;
			public Matrix4 modelview;
			public Matrix4 projection;
			public Matrix4 normal;
			public Vector4 LightPosition;
		}
		[StructLayout(LayoutKind.Sequential)]
		public struct VAOInstancesData
		{
			public Matrix4 modelMat;
			public Vector4 color;

			public VAOInstancesData(Matrix4 _modelMat, Vector4 _color){
				modelMat = _modelMat;
				color = _color;
			}
		}
			
		public Matrix4 Modelview {
			get { return shaderSharedData.modelview; }
			set { 
				shaderSharedData.modelview = value;
				shaderSharedData.normal = shaderSharedData.modelview.Inverted();
				shaderSharedData.normal.Transpose ();
				shaderSharedData.LightPosition = Vector4.Transform(vLight,	shaderSharedData.modelview);

				UBOisDirty = true;
			}
		}
		public Matrix4 Projection {
			get { return shaderSharedData.projection; }
			set { 
				shaderSharedData.projection = value; 

				UBOisDirty = true;
			}
		}
		public Vector4 VLight {
			set { 
				shaderSharedData.LightPosition = Vector4.Transform(value,	shaderSharedData.modelview);

				UBOisDirty = true;
			}
		}

		public Vector4 vLight = new Vector4 (0.5f, 0.5f, -0.7f, 0f);

		Mat4InstancedShader piecesShader;
		InstancedVAO<MeshData,VAOInstancesData> vaoGrouped;
		UBOSharedData shaderSharedData;
		int uboId;
		public volatile bool UBOisDirty = false;

		MeshPointer mpPawn, mpQueen, mpKing;
		InstancesVBO<VAOInstancesData> pawns, queens, kings;

		#region CTOR
		public Rendering ()
		{
			init ();
		}
		#endregion

		void init(){
			GL.Enable (EnableCap.CullFace);
			GL.CullFace (CullFaceMode.Back);
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);
			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			piecesShader = new Mat4InstancedShader();
			initShaderSharedDatas ();

			//vaoGrouped = new InstancedVAO<MeshData, VAOInstancesData> ();
		}

		public void Render(){
			if (vaoGrouped == null)
				return;
			piecesShader.Enable ();
			piecesShader.SetSimpleTexturedPass ();

			vaoGrouped.Bind ();
			vaoGrouped.Render (BeginMode.Triangles, mpPawn, pawns);
			vaoGrouped.Unbind ();
		}
		public void Update(){
			if (UBOisDirty)
				updateUBO ();			
		}


		void initShaderSharedDatas(){
			shaderSharedData.Color = new Vector4(1,1,1,1);
			uboId = GL.GenBuffer ();
			GL.BindBuffer (BufferTarget.UniformBuffer, uboId);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
				ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			GL.BindBufferBase (BufferRangeTarget.UniformBuffer, 0, uboId);
		}
		void updateUBO(){
			GL.BindBuffer (BufferTarget.UniformBuffer, uboId);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
				ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			UBOisDirty = false;
		}
	}
}


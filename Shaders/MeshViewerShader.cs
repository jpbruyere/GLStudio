//
//  MeshViewerShader.cs
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
using OpenTK.Graphics.OpenGL;
using OpenTK;

namespace GLStudio
{
	public class MeshViewerShader : Tetra.Shader
	{
		public MeshViewerShader ()
		{
		}
		public override void Init ()
		{
			vertSource = @"
			#version 330
			precision lowp float;

			uniform mat4 mvp;
			uniform mat4 modelView;
			uniform mat4 normalMat;			

			layout(location = 0) in vec3 in_position;
			layout(location = 1) in vec2 in_tex;
			layout (location = 2) in vec3 in_normal;

			out vec2 texCoord;
			out vec3 n;			
			out vec4 vEyeSpacePos;

			void main(void)
			{
				texCoord = in_tex;
				n = vec3(normalMat * vec4(in_normal, 0));
				vEyeSpacePos = modelView * vec4(in_position, 1);
				gl_Position = mvp * vec4(in_position, 1.0);
			}";

			fragSource = @"
			#version 330
			precision lowp float;

			uniform sampler2D tex;
			uniform vec3 diffuse = vec3(1.0, 1.0, 1.0);
			uniform vec3 ambient = vec3(0.3, 0.3, 0.3);
			uniform vec4 lightPos;

			in vec2 texCoord;
			in vec3 n;
			in vec4 vEyeSpacePos;

			out vec4 out_frag_color;

			void main(void)
			{
				vec3 vLight;
				vec3 vEye = normalize(-vEyeSpacePos.xyz);

				if (lightPos.w == 0.0)
					vLight = normalize(-lightPos.xyz);
				else
					vLight = normalize(lightPos.xyz - vEyeSpacePos.xyz);

				//blinn phong
				vec3 halfDir = normalize(vLight + vEye);
				//float specAngle = max(dot(halfDir, n), 0.0);
				//vec3 Ispec = specular * pow(specAngle, shininess);
				vec3 Idiff = diffuse * max(dot(n,vLight), 0.0);

				out_frag_color = vec4(ambient + Idiff,1.0);
			}";
			base.Init ();
		}

		int normalMatLoc, lightPosLoc, modelViewLoc;

		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
		}

		protected override void GetUniformLocations ()
		{
			base.GetUniformLocations ();
			normalMatLoc = GL.GetUniformLocation(pgmId, "normalMat");
			modelViewLoc = GL.GetUniformLocation(pgmId, "modelView");
			lightPosLoc =  GL.GetUniformLocation(pgmId, "lightPos");
		}
		public void SetNormalMat(Matrix4 _mat){
			GL.UniformMatrix4(normalMatLoc, false, ref _mat);
		}
		public void SetModelView(Matrix4 _mat){
			GL.UniformMatrix4(modelViewLoc, false, ref _mat);
		}
		public void SetLightPos(Vector4 _light){
			GL.Uniform4(lightPosLoc,_light);
		}
	}
}


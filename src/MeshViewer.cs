//
//  MeshViewer.cs
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
using Crow;
using Tetra.DynamicShading;
using System.Threading;
using OpenTK.Platform;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Diagnostics;
using GGL;
using System.Xml.Serialization;
using System.ComponentModel;

namespace GLStudio
{
	public class MeshViewer : GraphicObject , IDisposable
	{
		#region IDisposable implementation

		public void Dispose ()
		{
			run = false;
		}

		#endregion

		#region CTOR
		public MeshViewer () : base() {			
		}
		#endregion

		IWindowInfo windowInfo;
		IGraphicsContext glContext;

		Mesh<MeshData> mesh;
		string meshPath;
		bool wireframe;
		int texture;
		byte[] resultBitmap;

		Thread loadingThread, renderingThread;
		volatile bool meshLoadingInProgress = false, updateFBO = false, updateMesh=false, resetFBO = false, run=true;
		Stopwatch loadingTime = new Stopwatch();

		Matrix4 projection, modelview;
		Vector3 vEyeTarget = new Vector3(0f, 0f, 0.0f);
		Vector3 vLook = Vector3.Normalize(new Vector3(-0.5f, 0.5f, 0.5f));
		Vector4 vLightPos = new Vector4 (0.5f, -0.5f, -0.5f, 0f).Normalized();
		float eyeDist = 250f;

		const float zNear = 0.1f, zFar = 300.0f;
		const float fovY = (float)Math.PI / 4;
		const float moveSpeed = 0.02f, rotationSpeed = 0.02f, ZoomSpeed = 0.1f;
		float zoom = 1f;

		public string MeshPath {
			get { return meshPath; }
			set {
				if (meshPath == value)
					return;
				meshPath = value;
				if (meshLoadingInProgress) {
					try {
						loadingThread.Interrupt ();
						loadingThread.Join ();
						loadingThread.Abort ();
					} catch (Exception ex) {
						Console.WriteLine (ex.ToString ());
					}
				}
				NotifyValueChanged("MeshPath", meshPath);
				NotifyValueChanged ("VertCount", "-");
				NotifyValueChanged ("IndCount", "-");
				NotifyValueChanged ("DupVertCount", "-");

				if (string.IsNullOrEmpty (meshPath))
					return;
				if (!System.IO.File.Exists (meshPath))
					return;
				loadingThread = new Thread(loadNewMeshThread);
				loadingThread.IsBackground = true;
				loadingThread.Start ();
			}
		}

		[XmlAttributeAttribute][DefaultValue(false)]
		public virtual bool Wireframe {
			get { return wireframe; }
			set {
				if (value == wireframe)
					return;

				wireframe = value;

				NotifyValueChanged ("Wireframe", wireframe);
				updateFBO = true;
			}
		}

		public void CreateGLContext(IWindowInfo _windowInfo){
			windowInfo = _windowInfo;

			renderingThread = new Thread(renderMeshThread);
			//renderingThread.Priority = ThreadPriority.BelowNormal;
			renderingThread.IsBackground = true;
			renderingThread.Start ();
		}

		void updateViewMatrix()
		{
			Rectangle r = this.ClientRectangle;
			//projection = Matrix4.CreatePerspectiveFieldOfView (fovY, , zNear, zFar);
			float ratio = r.Height / (float)r.Width;
			float size = Math.Max (mesh.Bounds.Width, mesh.Bounds.Height)/ratio;
			Point<float> center = mesh.Bounds.Center;
			projection = Matrix4.CreateOrthographicOffCenter (
				center.X - size * zoom, center.X + size * zoom,
				center.Y + size * ratio * zoom,	center.Y - size * ratio * zoom,
				zNear, zFar);
			Vector3 vEye = vEyeTarget + vLook * eyeDist;
			modelview = Matrix4.LookAt(vEye, vEyeTarget, Vector3.UnitZ);
		}

		void loadNewMeshThread(){
			meshLoadingInProgress = true;
			loadingTime.Restart();
			mesh = Mesh<MeshData>.Load (meshPath);
			zoom = mesh.Bounds.Width;
			loadingTime.Stop ();
			meshLoadingInProgress = false;


			NotifyValueChanged ("VertCount", mesh.Positions.Length);
			NotifyValueChanged ("IndCount", mesh.Indices.Length);
			NotifyValueChanged ("DupVertCount", mesh.DuplicatedVerticesRemoved);
			NotifyValueChanged ("LoadTime", loadingTime.ElapsedMilliseconds);

			updateMesh = true;
			updateFBO = true;
		}
		void renderMeshThread(){
			using(glContext = new GraphicsContext (new GraphicsMode (new ColorFormat (32), 24, 0, 1),
				windowInfo, 3, 3, GraphicsContextFlags.Default)){
				glContext.MakeCurrent(windowInfo);

				GL.Enable (EnableCap.CullFace);
				GL.CullFace (CullFaceMode.Front);
				GL.Enable(EnableCap.DepthTest);
				GL.DepthFunc(DepthFunction.Less);
				GL.Enable (EnableCap.Blend);
				GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

				using (MeshViewerShader shader = new MeshViewerShader ()) {
					shader.Enable ();

					MeshVAO<MeshData> vao = null;

					while (run) {
						if (resetFBO)
							initFbo ();
						if (!updateFBO)
							continue;
						if (updateMesh){
							if (vao != null)
								vao.Dispose ();
							vao = new MeshVAO<MeshData> (mesh);
							updateMesh = false;
						}
						if (vao == null)
							continue;

						updateViewMatrix();

						shader.SetMVP (modelview * projection);
						Matrix4 normal = modelview.Inverted();
						normal.Transpose ();
						shader.SetNormalMat (normal);
						shader.SetLightPos (Vector4.Transform (vLightPos, modelview));

						GL.BindFramebuffer (FramebufferTarget.Framebuffer, fbo);

						GL.ClearColor (0.1f, 0.1f, 0.3f, 1.0f);
						GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
						GL.Viewport (0, 0, ClientRectangle.Width, ClientRectangle.Height);

						texture = Tetra.Texture.Load ("#GLStudio.images.board3.png");

						GL.BindTexture (TextureTarget.Texture2D, texture);
						vao.Bind ();

						if (Wireframe)
							vao.Render (BeginMode.LineStrip);
						else
							vao.Render (BeginMode.Triangles);
						
						vao.Unbind ();

						GL.BindFramebuffer (FramebufferTarget.Framebuffer, 0);

						GL.BindTexture (TextureTarget.Texture2D, fboTexture);
						lock (resultBitmap)
							GL.GetTexImage (TextureTarget.Texture2D, 0, PixelFormat.Bgra, PixelType.Byte, resultBitmap);
						GL.BindTexture (TextureTarget.Texture2D, 0);

						updateFBO = false;

						lock (CurrentInterface.LayoutMutex)
							RegisterForGraphicUpdate ();
					}

					deleteFbo ();
				}
			}
		}

		#region FBO

		int fboTexture, fbo, depthRenderbuffer;

		void deleteFbo()
		{
			if (GL.IsTexture (fboTexture)) {
				GL.DeleteTexture (fboTexture);
				GL.DeleteRenderbuffer (depthRenderbuffer);
				GL.DeleteFramebuffer (fbo);
			}
		}
		void initFbo()
		{
			deleteFbo ();

			Size cz = ClientRectangle.Size;

			resultBitmap = new byte[cz.Width * cz.Height *4];

			Tetra.Texture.DefaultMagFilter = TextureMagFilter.Nearest;
			Tetra.Texture.DefaultMinFilter = TextureMinFilter.Nearest;
			Tetra.Texture.GenerateMipMaps = false;
			{
				fboTexture = new Tetra.Texture (cz.Width, cz.Height);
			}
			Tetra.Texture.ResetToDefaultLoadingParams ();

			// Create Depth Renderbuffer
			GL.GenRenderbuffers( 1, out depthRenderbuffer );
			GL.BindRenderbuffer( RenderbufferTarget.Renderbuffer, depthRenderbuffer );
			GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, (RenderbufferStorage)All.DepthComponent32, cz.Width, cz.Height);

			GL.GenFramebuffers(1, out fbo);

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
			GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
				TextureTarget.Texture2D, fboTexture, 0);
			GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depthRenderbuffer );

			GL.DrawBuffers(1, new DrawBuffersEnum[]{DrawBuffersEnum.ColorAttachment0});

			if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
			{
				throw new Exception(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer).ToString());
			}

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			resetFBO = false;
			updateFBO = true;
		}

		#endregion

		#region Mouse
		public override void onMouseMove (object sender, MouseMoveEventArgs e)
		{
			base.onMouseMove (sender, e);

			if (e.XDelta != 0 || e.YDelta != 0)
			{
				if (e.Mouse.LeftButton == ButtonState.Pressed) {
					Vector3 v = new Vector3 (
						            Vector2.Normalize (vLook.Xy.PerpendicularLeft));
					vLook = vLook.Transform (
						Matrix4.CreateRotationZ (-e.XDelta * rotationSpeed) *
						Matrix4.CreateFromAxisAngle (v, -e.YDelta * rotationSpeed));
					vLook.Normalize ();
					updateFBO = true;
				}
			}
		}
		public override void onMouseWheel (object sender, MouseWheelEventArgs e)
		{
			base.onMouseWheel (sender, e);

			zoom -= e.Delta * ZoomSpeed;
			if (zoom <= 0f)
				zoom = ZoomSpeed;

			updateFBO = true;
		}
		#endregion

		void onExportMesh_MouseClick (object sender, MouseButtonEventArgs e)
		{
			if (mesh == null)
				return;
			string newPath = meshPath;
			newPath = 
				System.IO.Path.GetDirectoryName(newPath) +
				System.IO.Path.DirectorySeparatorChar +
				System.IO.Path.GetFileNameWithoutExtension (newPath) + ".bin";
			mesh.SaveAsBinary (newPath);
		}

		#region GraphicObject overrides
		protected override void onDraw (Cairo.Context gr)
		{
			base.onDraw (gr);
			if (resultBitmap == null || resetFBO)
				return;
			Rectangle r = ClientRectangle.Size;

			lock (resultBitmap) {
				using (Cairo.Surface surf = new Cairo.ImageSurface (resultBitmap, Cairo.Format.Argb32, r.Width, r.Height, r.Width * 4)) {
					gr.SetSourceSurface (surf, r.X, r.Y);
					gr.Paint ();
				}
			}
		}
		public override void OnLayoutChanges (LayoutingType layoutType)
		{
			base.OnLayoutChanges (layoutType);

			if (layoutType.HasFlag (LayoutingType.Width) | layoutType.HasFlag (LayoutingType.Height))
				resetFBO = true;			
		}
		#endregion
	}
}


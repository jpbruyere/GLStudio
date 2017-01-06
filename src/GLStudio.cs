using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Diagnostics;
using OpenTK;
using OpenTK.Graphics.OpenGL;

using Crow;
using GGL;
using Tetra.DynamicShading;
using System.Threading;

namespace GLStudio
{
	public struct TexturedMeshData
	{
		public Vector2[] TexCoords;

		public TexturedMeshData(Vector2[] _texCoord)
		{
			TexCoords = _texCoord;
		}
	}
	class GLStudio : OpenTKGameWindow
	{
		[STAThread]
		static void Main ()
		{
			GLStudio win = new GLStudio ();
			win.Run (30);
		}

		public GLStudio ()
			: base(800, 600,"GLStudio")
		{
		}
			
		public string RootDirectory {
			get { return Crow.Configuration.Get<string>("RootDirectory"); }
			set {
				if (RootDirectory == value)
					return;
				Crow.Configuration.Set ("RootDirectory", value);
				NotifyValueChanged ("RootDirectory", value);
			}
		} 

		object loadingMutex = new object();
		volatile bool meshLoadingInProgress = false;
		Mesh<TexturedMeshData> new_mesh;

		MeshVAO<TexturedMeshData> vaoTest;
		Tetra.Texture texture;

		void initGL(){
			GL.Enable (EnableCap.CullFace);
			GL.CullFace (CullFaceMode.Back);
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);
			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			texture = Tetra.Texture.Load (Crow.Configuration.Get<string> ("CurrentTexturePath"));

			LoadMesh(Crow.Configuration.Get<string> ("CurrentOBJPath"));

			UpdateViewMatrix();

		}

		void LoadMesh(string path){
			if (meshLoadingInProgress) {
				Console.WriteLine ("Cancel, Loading already in progress");
				return;
			}
			Thread t = new Thread(() => loadNewMeshThread(path));
			t.IsBackground = true;
			t.Start ();
		}

		void loadNewMeshThread(string path){			
			lock (loadingMutex) {
				meshLoadingInProgress = true;
				new_mesh = Mesh<TexturedMeshData>.Load (path);
			}
		}

		#region UI
		void initUI(){
			MouseMove += Mouse_Move;
			MouseButtonDown += Mouse_ButtonDown;
			MouseWheelChanged += Mouse_WheelChanged;
			KeyDown += KeyboardKeyDown1;

			CrowInterface.LoadInterface("#GLStudio.ui.mainMenu.crow").DataSource = this;
		}
		void rootDir_SelectedItemChanged (object sender, SelectionChangeEventArgs e)
		{
			FileSystemInfo f = e.NewValue as FileSystemInfo;
			if (f == null)
				return;
			FileInfo fi = f as FileInfo;
			if (fi == null)
				return;
			if (string.Equals (fi.Extension, ".obj", StringComparison.OrdinalIgnoreCase)) {
				Crow.Configuration.Set ("CurrentOBJPath", fi.FullName);
				LoadMesh (fi.FullName);
			}else if (string.Equals (fi.Extension, ".png", StringComparison.OrdinalIgnoreCase)||
				string.Equals (fi.Extension, ".gif", StringComparison.OrdinalIgnoreCase)||
				string.Equals (fi.Extension, ".bmp", StringComparison.OrdinalIgnoreCase)||
				string.Equals (fi.Extension, ".jpg", StringComparison.OrdinalIgnoreCase)|| 
				string.Equals (fi.Extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
			{
				try {
					texture = Tetra.Texture.Load (fi.FullName);
					Crow.Configuration.Set ("CurrentTexturePath", fi.FullName);
				} catch (Exception ex) {
					Console.WriteLine (ex.ToString ());
				}
			}
		}
		void exec_quit(object sender, EventArgs e){
			Quit (null, null);
		}
		void exec_explorer(object sender, EventArgs e){
			CrowInterface.LoadInterface ("#GLStudio.ui.GLExplorer.iml").DataSource = this;
		}
		void exec_crowPerfs(object sender, EventArgs e){
			CrowInterface.LoadInterface ("#GLStudio.ui.perfMeasures.crow").DataSource = this;
		}
		#endregion

		#region Game win overrides
		protected override void OnLoad (EventArgs e)
		{
			base.OnLoad (e);
			initUI ();
			initGL ();
		}

		protected override void OnUpdateFrame (OpenTK.FrameEventArgs e)
		{
			base.OnUpdateFrame (e);
			if (Monitor.TryEnter (loadingMutex)) {
				if (new_mesh != null) {
					vaoTest = new MeshVAO<TexturedMeshData> (new_mesh);
					new_mesh = null;
					meshLoadingInProgress = false;
				}
				Monitor.Exit (loadingMutex);
			}
		}
		public override void OnRender (FrameEventArgs e)
		{
			base.OnRender (e);
			if (vaoTest == null)
				return;
			shader.Enable ();
			shader.SetMVP(modelview * projection);

			GL.BindTexture (TextureTarget.Texture2D, texture);
			vaoTest.Render (BeginMode.Triangles);
			GL.BindTexture (TextureTarget.Texture2D, 0);
		}
		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);
			UpdateViewMatrix ();
		}
		#endregion


		#region  scene matrix and vectors
		public static Matrix4 modelview;
		public static Matrix4 projection;
//		public static Matrix4 orthoMat//full screen quad rendering
//		= OpenTK.Matrix4.CreateOrthographicOffCenter (-0.5f, 0.5f, -0.5f, 0.5f, 1, -1);
		public static int[] viewport = new int[4];

		public float EyeDist {
			get { return eyeDist; }
			set {
				eyeDist = value;
				UpdateViewMatrix ();
			}
		}
		public Vector3 vEyeTarget = new Vector3(0f, 0f, 0f);
		public Vector3 vEye;
		public Vector3 vLook = Vector3.Normalize(new Vector3(0.0f, -0.7f, 0.7f));
		public float zFar = 300.0f;
		public float zNear = 0.1f;
		public float fovY = (float)Math.PI / 4;

		float eyeDist = 10f;
		float eyeDistTarget = 10f;
		float MoveSpeed = 0.02f;
		float RotationSpeed = 0.005f;
		float ZoomSpeed = 2f;

		public Vector4 vLight = new Vector4 (0.5f, 0.5f, -1f, 0f);
		#endregion

		#region Mouse and Keyboard
		void KeyboardKeyDown1 (object sender, OpenTK.Input.KeyboardKeyEventArgs e)
		{
			if (e.Key == OpenTK.Input.Key.Escape) 
				Quit (null, null);
			else if (e.Key == OpenTK.Input.Key.F6) 
				CrowInterface.LoadInterface ("#GLStudio.ui.GLExplorer.iml").DataSource = this;
			else if (e.Key == OpenTK.Input.Key.F7) 
				CrowInterface.LoadInterface ("#GLStudio.ui.perfMeasures.crow").DataSource = this;			
		}

		void Mouse_ButtonDown (object sender, OpenTK.Input.MouseButtonEventArgs e)
		{
			if (e.Mouse.LeftButton != OpenTK.Input.ButtonState.Pressed)
				return;			
		}
		void Mouse_Move(object sender, OpenTK.Input.MouseMoveEventArgs e)
		{
			if (e.XDelta != 0 || e.YDelta != 0)
			{
				if (e.Mouse.MiddleButton == OpenTK.Input.ButtonState.Pressed) {
					Vector3 v = new Vector3 (
						Vector2.Normalize (vLook.Xy.PerpendicularLeft));
					vLook = vLook.Transform( 
						Matrix4.CreateRotationZ (-e.XDelta * RotationSpeed) *
						Matrix4.CreateFromAxisAngle (v, -e.YDelta * RotationSpeed));
//
//					vLook = vLook.Transform (Matrix4.CreateRotationZ(-(float)e.XDelta * RotationSpeed));
//					vLook = vLook.Transform (Matrix4.CreateFromAxisAngle(Vector2.per.Cross(vLook,
//						-(float)e.YDelta * RotationSpeed));
					vLook.Normalize();
					UpdateViewMatrix ();
				}else if (e.Mouse.LeftButton == OpenTK.Input.ButtonState.Pressed) {
					return;
				}else if (e.Mouse.RightButton == OpenTK.Input.ButtonState.Pressed) {
					vEyeTarget = vEyeTarget.Transform (Matrix4.CreateTranslation (-e.XDelta*MoveSpeed, e.YDelta*MoveSpeed, 0));
					UpdateViewMatrix();
				}
			}
		}
		public void UpdateViewMatrix()
		{
			Rectangle r = this.ClientRectangle;
			GL.Viewport( r.X, r.Y, r.Width, r.Height);
			projection = Matrix4.CreatePerspectiveFieldOfView (fovY, r.Width / (float)r.Height, zNear, zFar);
			vEye = vEyeTarget + vLook * eyeDist;
			modelview = Matrix4.LookAt(vEye, vEyeTarget, Vector3.UnitZ);
			GL.GetInteger(GetPName.Viewport, viewport);
		}
		void Mouse_WheelChanged(object sender, OpenTK.Input.MouseWheelEventArgs e)
		{
			float speed = ZoomSpeed;
			if (Keyboard[OpenTK.Input.Key.ShiftLeft])
				speed *= 0.1f;
			else if (Keyboard[OpenTK.Input.Key.ControlLeft])
				speed *= 20.0f;

			eyeDistTarget -= e.Delta * speed;
			if (eyeDistTarget < zNear+1)
				eyeDistTarget = zNear+1;
			else if (eyeDistTarget > zFar-6)
				eyeDistTarget = zFar-6;

			eyeDist = eyeDistTarget;
			UpdateViewMatrix ();
		}
		#endregion
	}
}

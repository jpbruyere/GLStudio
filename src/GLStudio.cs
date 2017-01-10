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
using System.Runtime.InteropServices;
using Tetra;
using System.Runtime.Serialization.Formatters.Binary;
using OpenTK.Graphics;

namespace GLStudio
{
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



		Rendering renderingContext;

		#region GL
		Vector3 vEyeTarget = new Vector3(0f, 0f, 0f);
		Vector3 vLook = Vector3.Normalize(new Vector3(0.0f, -0.7f, 0.7f));
		float eyeDist = 10f;

		const float zNear = 0.1f, zFar = 300.0f;
		const float fovY = (float)Math.PI / 4;
		const float MoveSpeed = 0.02f, RotationSpeed = 0.005f, ZoomSpeed = 2f;

		Vector4 vLight = new Vector4 (0.5f, 0.5f, -0.7f, 0f);

		void UpdateViewMatrix()
		{
			this.Context.MakeCurrent (this.WindowInfo);
			Rectangle r = this.ClientRectangle;
			GL.Viewport( r.X, r.Y, r.Width, r.Height);
			renderingContext.Projection = Matrix4.CreatePerspectiveFieldOfView (fovY, r.Width / (float)r.Height, zNear, zFar);
			Vector3 vEye = vEyeTarget + vLook * eyeDist;
			renderingContext.Modelview = Matrix4.LookAt(vEye, vEyeTarget, Vector3.UnitZ);
		}


		#endregion

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
			if (string.Equals (fi.Extension, ".obj", StringComparison.OrdinalIgnoreCase)||
				string.Equals (fi.Extension, ".bin", StringComparison.OrdinalIgnoreCase)) {
				GraphicObject win = CrowInterface.LoadInterface ("#GLStudio.ui.vaoDetails.crow");
				win.DataSource = this;
				MeshViewer mv = win.FindByName ("Mesh") as MeshViewer;
				mv.CreateGLContext (this.WindowInfo);
				mv.MeshPath = fi.FullName;
				//NotifyValueChanged ("NewMeshPath", fi.FullName);
//				Crow.Configuration.Set ("CurrentOBJPath", fi.FullName);
//				LoadMesh (fi.FullName);
			}else if (string.Equals (fi.Extension, ".png", StringComparison.OrdinalIgnoreCase)||
				string.Equals (fi.Extension, ".gif", StringComparison.OrdinalIgnoreCase)||
				string.Equals (fi.Extension, ".bmp", StringComparison.OrdinalIgnoreCase)||
				string.Equals (fi.Extension, ".jpg", StringComparison.OrdinalIgnoreCase)|| 
				string.Equals (fi.Extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
			{
//				try {
//					texture = Tetra.Texture.Load (fi.FullName);
//					Crow.Configuration.Set ("CurrentTexturePath", fi.FullName);
//				} catch (Exception ex) {
//					Console.WriteLine (ex.ToString ());
//				}
			}
			Button b;

		}

		void exec_quit(object sender, EventArgs e){
			Quit (null, null);
		}
		void exec_explorer(object sender, EventArgs e){
			CrowInterface.LoadInterface ("#GLStudio.ui.GLExplorer.iml").DataSource = this;
		}
		void exec_viewVAOStat(object sender, EventArgs e){
			CrowInterface.LoadInterface ("#GLStudio.ui.vaoDetails.crow").DataSource = this;
		}
		void exec_crowPerfs(object sender, EventArgs e){
			CrowInterface.LoadInterface ("#GLStudio.ui.perfMeasures.crow").DataSource = this;
		}
		void onMeshViewClose(object sender, EventArgs e){
			((sender as GraphicObject).FindByName ("Mesh") as MeshViewer).Dispose ();
		}

		#endregion

		#region Game win overrides
		protected override void OnLoad (EventArgs e)
		{
			base.OnLoad (e);
			initUI ();
			renderingContext = new Rendering ();
		}

		protected override void OnUpdateFrame (OpenTK.FrameEventArgs e)
		{
			this.Context.MakeCurrent (this.WindowInfo);

			base.OnUpdateFrame (e);

			renderingContext.Update ();
		}
		public override void OnRender (FrameEventArgs e)
		{
			this.Context.MakeCurrent (this.WindowInfo);
			base.OnRender (e);
			renderingContext.Render ();
		}
		protected override void OnResize (EventArgs e)
		{			
			base.OnResize (e);
			UpdateViewMatrix ();
		}
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
		void Mouse_WheelChanged(object sender, OpenTK.Input.MouseWheelEventArgs e)
		{
			float speed = ZoomSpeed;
			if (Keyboard[OpenTK.Input.Key.ShiftLeft])
				speed *= 0.1f;
			else if (Keyboard[OpenTK.Input.Key.ControlLeft])
				speed *= 20.0f;

			eyeDist -= e.Delta * speed;
			if (eyeDist < zNear+1)
				eyeDist = zNear+1;
			else if (eyeDist > zFar-6)
				eyeDist = zFar-6;

			UpdateViewMatrix ();
		}
		#endregion
	}
}

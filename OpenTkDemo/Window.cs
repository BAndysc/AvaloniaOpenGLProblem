using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp.PixelFormats;
using Vector3 = System.Numerics.Vector3;

namespace OpenTkDemo
{
    public class Window : GameWindow
    {
        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            using (var sr = new BinaryReader(File.Open("teapot.bin", FileMode.Open)))
            {
                var buf = new byte[sr.ReadInt32()];
                sr.Read(buf, 0, buf.Length);
                var points = new float[buf.Length / 4];
                global::System.Buffer.BlockCopy(buf, 0, points, 0, buf.Length);
                buf = new byte[sr.ReadInt32()];
                sr.Read(buf, 0, buf.Length);
                _indices = new ushort[buf.Length / 2];
                global::System.Buffer.BlockCopy(buf, 0, _indices, 0, buf.Length);
                _points = new Vertex[points.Length / 3];
                for (var primitive = 0; primitive < points.Length / 3; primitive++)
                {
                    var srci = primitive * 3;
                    _points[primitive] = new Vertex
                    {
                        Position = new Vector3(points[srci], points[srci + 1], points[srci + 2])
                    };
                }

                for (int i = 0; i < _indices.Length; i += 3)
                {
                    Vector3 a = _points[_indices[i]].Position;
                    Vector3 b = _points[_indices[i + 1]].Position;
                    Vector3 c = _points[_indices[i + 2]].Position;
                    var normal = Vector3.Normalize(Vector3.Cross(c - b, a - b));

                    _points[_indices[i]].Normal += normal;
                    _points[_indices[i + 1]].Normal += normal;
                    _points[_indices[i + 2]].Normal += normal;
                }

                for (int i = 0; i < _points.Length; i++)
                {
                    _points[i].Normal = Vector3.Normalize(_points[i].Normal);
                    _maxY = Math.Max(_maxY, _points[i].Position.Y);
                    _minY = Math.Min(_minY, _points[i].Position.Y);
                }
            }
        }

        
        private float _yaw;
        public float Yaw
        {
            get => _yaw;
        }

        private float _pitch;
        public float Pitch
        {
            get => _pitch;
        }
        
        private float _roll;
        public float Roll
        {
            get => _roll;
        }
        
        private float _disco;
        public float Disco
        {
            get => _disco;
        }

        private int _vertexShader;
        private int _fragmentShader;
        private int _shaderProgram;
        private int _vertexBufferObject;
        private int _indexBufferObject;
        private int _vertexArrayObject;
        private int _textureHandle;
        
        private string GetShader(bool fragment, string shader)
        {
            var version = 330;
            var data = "#version " + version + "\n";
            if (version >= 150)
            {
                shader = shader.Replace("attribute", "in");
                if (fragment)
                    shader = shader
                        .Replace("varying", "in")
                        .Replace("//DECLAREGLFRAG", "out vec4 outFragColor;")
                        .Replace("gl_FragColor", "outFragColor");
                else
                    shader = shader.Replace("varying", "out");
            }

            data += shader;

            return data;
        }


        private string VertexShaderSource => GetShader(false, @"
        attribute vec3 aPos;
        attribute vec3 aNormal;
        uniform mat4 uModel;
        uniform mat4 uProjection;
        uniform mat4 uView;

        varying vec3 FragPos;
        varying vec3 VecPos;  
        varying vec3 Normal;
        uniform float uTime;
        uniform float uDisco;
        void main()
        {
            float discoScale = sin(uTime * 10.0) / 10.0;
            float distortionX = 1.0 + uDisco * cos(uTime * 20.0) / 10.0;
            
            float scale = 1.0 + uDisco * discoScale;
            
            vec3 scaledPos = aPos;
            scaledPos.x = scaledPos.x * distortionX;
            
            scaledPos *= scale;
            gl_Position = uProjection * uView * uModel * vec4(scaledPos, 1.0);
            FragPos = vec3(uModel * vec4(aPos, 1.0));
            VecPos = aPos;
            Normal = normalize(vec3(uModel * vec4(aNormal, 1.0)));
        }
");

        private string FragmentShaderSource => GetShader(true, @"
        varying vec3 FragPos; 
        varying vec3 VecPos; 
        varying vec3 Normal;
        uniform float uMaxY;
        uniform float uMinY;
        uniform float uTime;
        uniform float uDisco;
        uniform sampler2D texture0;
        //DECLAREGLFRAG

        void main()
        {
            float y = (VecPos.y - uMinY) / (uMaxY - uMinY);
            float c = cos(atan(VecPos.x, VecPos.z) * 20.0 + uTime * 40.0 + y * 50.0);
            float s = sin(-atan(VecPos.z, VecPos.x) * 20.0 - uTime * 20.0 - y * 30.0);

            vec3 discoColor = vec3(
                0.5 + abs(0.5 - y) * cos(uTime * 10.0),
                0.25 + (smoothstep(0.3, 0.8, y) * (0.5 - c / 4.0)),
                0.25 + abs((smoothstep(0.1, 0.4, y) * (0.5 - s / 4.0))));

            vec3 objectColor = vec3((1.0 - y), 0.40 +  y / 4.0, y * 0.75 + 0.25);
            objectColor = objectColor * (1.0 - uDisco) + discoColor * uDisco;

            objectColor = texture(texture0, FragPos.xy / 5).rgb;

            float ambientStrength = 0.3;
            vec3 lightColor = vec3(1.0, 1.0, 1.0);
            vec3 lightPos = vec3(uMaxY * 2.0, uMaxY * 2.0, uMaxY * 2.0);
            vec3 ambient = ambientStrength * lightColor;


            vec3 norm = normalize(Normal);
            vec3 lightDir = normalize(lightPos - FragPos);  

            float diff = max(dot(norm, lightDir), 0.0);
            vec3 diffuse = diff * lightColor;

            vec3 result = (ambient + diffuse) * objectColor;
            gl_FragColor = vec4(result, 1.0);

        }
");

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        private readonly Vertex[] _points;
        private readonly ushort[] _indices;
        private readonly float _minY;
        private readonly float _maxY;
        
        private void CheckError()
        {
            ErrorCode err;
            while ((err = GL.GetError()) != ErrorCode.NoError)
                Console.WriteLine(err);
        }

        static Stopwatch St = Stopwatch.StartNew();
        
        protected override unsafe void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            CheckError();
            this.Title = $"Renderer: {GL.GetString(StringName.Renderer)} Version: {GL.GetString(StringName.Version)}";

            int[] textures = new int[1];
            GL.GenTextures(1, textures);
            _textureHandle = textures[0];
            GL.BindTexture(TextureTarget.Texture2D, _textureHandle);
            using (SixLabors.ImageSharp.Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>("logo.png"))
            {
                if (!image.TryGetSinglePixelSpan(out var pixels))
                    throw new Exception();
                fixed (void* pdata = pixels)
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, new IntPtr(pdata));
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);

            // Load the source of the vertex shader and compile it.
            _vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(_vertexShader, VertexShaderSource);
            GL.CompileShader(_vertexShader);

            // Load the source of the fragment shader and compile it.
            _fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(_fragmentShader, FragmentShaderSource);
            GL.CompileShader(_fragmentShader);

            // Create the shader program, attach the vertex and fragment shaders and link the program.
            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, _vertexShader);
            GL.AttachShader(_shaderProgram, _fragmentShader);
            const int positionLocation = 0;
            const int normalLocation = 1;
            GL.BindAttribLocation(_shaderProgram, positionLocation, "aPos");
            GL.BindAttribLocation(_shaderProgram, normalLocation, "aNormal");
            GL.LinkProgram(_shaderProgram);
            CheckError();

            // Create the vertex buffer object (VBO) for the vertex data.
            _vertexBufferObject = GL.GenBuffer();
            // Bind the VBO and copy the vertex data into it.
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            CheckError();
            var vertexSize = Marshal.SizeOf<Vertex>();
            fixed (void* pdata = _points)
                GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(_points.Length * vertexSize),
                    new IntPtr(pdata), BufferUsageHint.StaticDraw);

            _indexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBufferObject);
            CheckError();
            fixed (void* pdata = _indices)
                GL.BufferData(BufferTarget.ElementArrayBuffer, new IntPtr(_indices.Length * sizeof(ushort)), new IntPtr(pdata),
                    BufferUsageHint.StaticDraw);
            CheckError();
            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);
            CheckError();
            GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, vertexSize, 0);
            GL.VertexAttribPointer(normalLocation, 3, VertexAttribPointerType.Float, false, vertexSize, 12);
            GL.EnableVertexAttribArray(positionLocation);
            GL.EnableVertexAttribArray(normalLocation);
            CheckError();
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            GL.BindTexture(TextureTarget.Texture2D, 0);
            
            // Unbind everything
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // Delete all resources.
            int[] textures = new int[] { _textureHandle };
            GL.DeleteTextures(1, textures);
            GL.DeleteBuffers(2, new[] { _vertexBufferObject, _indexBufferObject });
            GL.DeleteVertexArrays(1, new[] { _vertexArrayObject });
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteShader(_fragmentShader);
            GL.DeleteShader(_vertexShader);
        }

        protected override unsafe void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Viewport(0, 0, Size.X, Size.Y);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBufferObject);
            GL.BindVertexArray(_vertexArrayObject);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _textureHandle);
            GL.UseProgram(_shaderProgram);
            CheckError();
            var projection =
                Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 4), (float)(Size.X * 1.0 / Size.Y),
                    0.01f, 1000);


            var view = Matrix4x4.CreateLookAt(new Vector3(25, 25, 25), new Vector3(), new Vector3(0, -1, 0));
            var model = Matrix4x4.CreateFromYawPitchRoll(_yaw, _pitch, _roll);
            var modelLoc = GL.GetUniformLocation(_shaderProgram, "uModel");
            var viewLoc = GL.GetUniformLocation(_shaderProgram, "uView");
            var projectionLoc = GL.GetUniformLocation(_shaderProgram, "uProjection");
            var maxYLoc = GL.GetUniformLocation(_shaderProgram, "uMaxY");
            var minYLoc = GL.GetUniformLocation(_shaderProgram, "uMinY");
            var timeLoc = GL.GetUniformLocation(_shaderProgram, "uTime");
            var discoLoc = GL.GetUniformLocation(_shaderProgram, "uDisco");
            var texture0Loc = GL.GetUniformLocation(_shaderProgram, "texture0");

            var p = new float[]{projection.M11, projection.M12, projection.M13, projection.M14,
                projection.M21, projection.M22, projection.M23, projection.M24,
                projection.M31, projection.M32, projection.M33, projection.M34,
                projection.M41, projection.M42, projection.M43, projection.M44};
            var v = new float[]{view.M11, view.M12, view.M13, view.M14,
                view.M21, view.M22, view.M23, view.M24,
                view.M31, view.M32, view.M33, view.M34,
                view.M41, view.M42, view.M43, view.M44};
            var m = new float[]{model.M11, model.M12, model.M13, model.M14,
                model.M21, model.M22, model.M23, model.M24,
                model.M31, model.M32, model.M33, model.M34,
                model.M41, model.M42, model.M43, model.M44};
            fixed (float* ptr = m)
                GL.UniformMatrix4(modelLoc, 1, false, ptr);
            fixed (float* ptr = v)
                GL.UniformMatrix4(viewLoc, 1, false, ptr);
            fixed (float* ptr = p)
                GL.UniformMatrix4(projectionLoc, 1, false, ptr);
            GL.Uniform1(maxYLoc, _maxY);
            GL.Uniform1(minYLoc, _minY);
            GL.Uniform1(timeLoc, (float)St.Elapsed.TotalSeconds);
            GL.Uniform1(discoLoc, _disco);
            GL.Uniform1(texture0Loc, 1);
            CheckError();
            GL.DrawElements(BeginMode.Triangles, _indices.Length, DrawElementsType.UnsignedShort, 0);

            CheckError();
            
            SwapBuffers();
        }

        // protected override void OnResize(ResizeEventArgs e)
        // {
        //     base.OnResize(e);
        //
        //     GL.Viewport(0, 0, Size.X, Size.Y);
        // }
    }
}
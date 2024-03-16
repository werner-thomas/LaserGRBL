﻿using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.SceneGraph.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Serialization;

namespace LaserGRBL.Obj3D
{
    public class Object3DVertex
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public GLColor OriginalColor { get; set; }
        public GLColor NewColor { get; set; }
        public GrblCommand Command { get; set; }
    }

    public class Object3DDisplayList
    {
        public bool IsValid { get; private set; } = true;
        public DisplayList DisplayList { get; private set; }
        public List<Object3DVertex> Vertices { get; } = new List<Object3DVertex>();

        public void Begin(OpenGL gl, float lineWidth)
        {
            DisplayList = new DisplayList();
            DisplayList.Generate(gl);
            DisplayList.New(gl, DisplayList.DisplayListMode.Compile);
            gl.PushAttrib(OpenGL.GL_CURRENT_BIT | OpenGL.GL_ENABLE_BIT | OpenGL.GL_LINE_BIT);
            gl.Disable(OpenGL.GL_LIGHTING);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.LineWidth(lineWidth);
            gl.Begin(OpenGL.GL_LINES);
        }

        public void End(OpenGL gl)
        {
            //gl.Disable(OpenGL.GL_POLYGON_SMOOTH);
            gl.End();
            gl.PopAttrib();
            DisplayList.End(gl);
        }

        internal void Invalidate() => IsValid = false;

        internal void Invalidated() => IsValid = true;

    }

    public abstract class Object3D : SceneElement, IRenderable, IDisposable
    {
        [XmlIgnore]
        protected List<Object3DDisplayList> mDisplayLists = new List<Object3DDisplayList>();
        [XmlIgnore]
        protected float mLineWidth;
        [XmlIgnore]
        protected OpenGL mGL;
        [XmlIgnore]
        protected Object3DDisplayList mCurrentDisplayList = null;
        [XmlIgnore]
        protected const int MAX_VECTOR_IN_DISPLAY_LIST = 1000;

        public Object3D(string name, float lineWidth)
        {
            Name = name;
            mLineWidth = lineWidth;
        }

        protected virtual void ClearDisplayList()
        {
            foreach (Object3DDisplayList list in mDisplayLists)
            {
                list.DisplayList.Delete(mGL);
            }
            mDisplayLists.Clear();
        }

        public virtual void Render(OpenGL gl, RenderMode renderMode)
        {
            if (renderMode == RenderMode.Design)
            {
                if (mDisplayLists.Count <= 0)
                {
                    mGL = gl;
                    CreateDisplayList();
                }
                CallDisplayList(gl);
            }
        }

        protected virtual void CallDisplayList(OpenGL gl)
        {
            foreach (Object3DDisplayList object3DDisplayList in mDisplayLists)
            {
                object3DDisplayList.DisplayList.Call(gl);
            }
        }

        private void CreateDisplayList()
        {
            mCurrentDisplayList = new Object3DDisplayList();
            mCurrentDisplayList.Begin(mGL, mLineWidth);
            mDisplayLists.Add(mCurrentDisplayList);
            Draw();
            mCurrentDisplayList.End(mGL);
            mCurrentDisplayList = null;
        }

        protected abstract void Draw();

        protected void NewDisplayList()
        {
            mCurrentDisplayList.End(mGL);
            mCurrentDisplayList = new Object3DDisplayList();
            mCurrentDisplayList.Begin(mGL, mLineWidth);
            mDisplayLists.Add(mCurrentDisplayList);
        }

        public void AddVertex(double x, double y, double z, GLColor color, GrblCommand command = null)
        {
            mGL.Color(color);
            mGL.Vertex(x, y, z);
            mCurrentDisplayList.Vertices.Add(new Object3DVertex { X = x, Y = y, Z = z, OriginalColor = color, Command = command });
            if (command != null) command.LinkedDisplayList = mCurrentDisplayList;
        }

        public void CheckListSize()
        {
            if (mCurrentDisplayList.Vertices.Count > MAX_VECTOR_IN_DISPLAY_LIST) NewDisplayList();
        }

        public void Dispose()
        {
            foreach (Object3DDisplayList object3DDisplayList in mDisplayLists)
            {
                object3DDisplayList.Vertices.Clear();
            }
            ClearDisplayList();
        }

    }

    public class Grid3D : Object3D
    {
        [XmlIgnore]
        public int MaxWidth { get; set; } = 2000;
        [XmlIgnore]
        public int MaxHeight { get; set; } = 2000;
        [XmlIgnore]
        private DisplayList mDisplayOrigins;
        [XmlIgnore]
        private DisplayList mDisplayTick;
        [XmlIgnore]
        private DisplayList mDisplayMinor;
        [XmlIgnore]
        public bool ShowMinor { get; set; } = false;
        [XmlIgnore]
        private GLColor mTicksColor = new GLColor();
        [XmlIgnore]
        public GLColor TicksColor {
            get => mTicksColor;
            set
            {
                if (value != mTicksColor)
                {
                    mTicksColor = value;
                    ClearDisplayList();
                }
            }
        }
        [XmlIgnore]
        private GLColor mMinorsColor = new GLColor();
        [XmlIgnore]
        public GLColor MinorsColor
        {
            get => mMinorsColor;
            set
            {
                if (value != mMinorsColor)
                {
                    mMinorsColor = value;
                    ClearDisplayList();
                }
            }
        }
        [XmlIgnore]
        private GLColor mOriginsColor = new GLColor();
        [XmlIgnore]
        public GLColor OriginsColor
        {
            get => mOriginsColor;
            set
            {
                if (value != mOriginsColor)
                {
                    mOriginsColor = value;
                    ClearDisplayList();
                }
            }
        }


        public Grid3D() : base("Grid", 0.01f) { }

        private void DrawCross(float position, GLColor color, float z)
        {
            AddVertex(position, -MaxHeight, z, color);
            AddVertex(position, MaxHeight, z, color);
            AddVertex(-MaxHeight, position, z, color);
            AddVertex(MaxHeight, position, z, color);
        }

        protected override void Draw()
        {
            // minor tick display list
            mDisplayMinor = mCurrentDisplayList.DisplayList;
            for (int i = -MaxWidth; i <= MaxWidth; i++)
            {
                if (i % 10 == 0 || i == 0) continue;
                DrawCross(i, MinorsColor, -20f);
            }
            // tick display list
            NewDisplayList();
            mDisplayTick = mCurrentDisplayList.DisplayList;
            for (int i = -MaxWidth; i <= MaxWidth; i++)
            {
                if (i % 10 != 0 || i == 0) continue;
                DrawCross(i, TicksColor, -10f);
            }
            // oringins display list
            NewDisplayList();
            mDisplayOrigins = mCurrentDisplayList.DisplayList;
            DrawCross(0, OriginsColor, -1f);
        }

        protected override void CallDisplayList(OpenGL gl)
        {
            mDisplayOrigins.Call(gl);
            mDisplayTick.Call(gl);
            if (ShowMinor)
            {
                mDisplayMinor.Call(gl);
            }
        }

    }

    public class Grbl3D : Object3D
    {

        [XmlIgnore]
        public readonly GrblFile File;
        [XmlIgnore]
        private readonly bool mJustLaserOffMovements;
        [XmlIgnore]
        private readonly Color mColor;


        public Grbl3D(GrblFile file, string name, bool justLaserOffMovements, Color color) : base(name, 1f)
        {
            File = file;
            mJustLaserOffMovements = justLaserOffMovements;
            mColor = color;
        }

        protected override void Draw()
        {
            float zPos = 0;
            GrblCommand.StatePositionBuilder spb = new GrblCommand.StatePositionBuilder();
            for (int i = 0; i < File.Commands.Count; i++)
            {
                GrblCommand cmd = File.Commands[i];
                try
                {
                    cmd.BuildHelper();
                    spb.AnalyzeCommand(cmd, false);

                    if (spb.TrueMovement())
                    {
                        if (spb.G0G1 && cmd.IsLinearMovement)
                        {
                            GLColor color = null;
                            if (spb.LaserBurning && !mJustLaserOffMovements)
                            {
                                color = spb.GetCurrentColor(File.Range.SpindleRange, mColor);
                            }
                            else
                            {
                                if (mJustLaserOffMovements)
                                {
                                    color = mColor;
                                }
                            }
                            if (color != null)
                            {
                                AddVertex((float)spb.X.Previous, (float)spb.Y.Previous, zPos, color, cmd);
                                AddVertex((float)spb.X.Number, (float)spb.Y.Number, zPos, color, cmd);
                            }
                        }
                        else if (spb.G2G3 && cmd.IsArcMovement)
                        {
                            GrblCommand.G2G3Helper ah = spb.GetArcHelper(cmd);
                            if (ah.RectW > 0 && ah.RectH > 0)
                            {
                                double? lastX = null;
                                double? lastY = null;
                                GLColor color = spb.GetCurrentColor(File.Range.SpindleRange, mColor);
                                double startAngle = ah.StartAngle;
                                double endAngle = ah.StartAngle + ah.AngularWidth;
                                int sign = Math.Sign(ah.AngularWidth);
                                double angleStep = Math.Abs(ah.AngularWidth / Math.PI / 36);
                                for (double angle = startAngle; sign * (angle - startAngle) <= sign * ah.AngularWidth; angle += sign * angleStep)
                                {
                                    double x = ah.CenterX + ah.RectW / 2 * Math.Cos(angle);
                                    double y = ah.CenterY + ah.RectH / 2 * Math.Sin(angle);
                                    if (lastX != null && lastY != null)
                                    {
                                        AddVertex((double)lastX, (double)lastY, zPos, color, cmd);
                                    }
                                    else
                                    {
                                        AddVertex(x, y, zPos, color, cmd);
                                    }
                                    AddVertex(x, y, zPos, color, cmd);
                                    lastX = x;
                                    lastY = y;
                                }
                            }
                        }
                        CheckListSize();
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally { cmd.DeleteHelper(); }
            }
        }

        public void Invalidate()
        {
            if (mJustLaserOffMovements) return;
            int invalidatedLists = 0;
            foreach (Object3DDisplayList list in mDisplayLists)
            {
                if (!list.IsValid) {
                    invalidatedLists++;
                    list.DisplayList.Delete(mGL);
                    list.Begin(mGL, mLineWidth);
                    foreach (Object3DVertex vertex in list.Vertices)
                    {
                        GLColor newColor;
                        switch (vertex.Command.Status)
                        {
                            case GrblCommand.CommandStatus.ResponseGood:
                                newColor = new GLColor(0.1f, 1f, 0.2f, 20);
                                break;
                            case GrblCommand.CommandStatus.ResponseBad:
                            case GrblCommand.CommandStatus.InvalidResponse:
                                newColor = Color.Red;
                                break;
                            case GrblCommand.CommandStatus.Queued:
                                newColor = Color.Blue;
                                break;
                            case GrblCommand.CommandStatus.WaitingResponse:
                                newColor = Color.Pink;
                                break;
                            default:
                                newColor = vertex.OriginalColor;
                                break;
                        }
                        vertex.NewColor = vertex.OriginalColor.Blend(newColor);
                        mGL.Color(vertex.NewColor);
                        mGL.Vertex(vertex.X, vertex.Y, vertex.Z);
                    }
                    list.End(mGL);
                    list.Invalidated();
                }
            }
        }

        public void ResetColor()
        {
            foreach (GrblCommand command in File.Commands)
            {
                command.SetResult("CLEAR", false);
            }
            foreach (Object3DDisplayList list in mDisplayLists)
            {
                foreach (Object3DVertex vertex in list.Vertices)
                {
                    vertex.NewColor = null;
                }
                list.Invalidate();
            }
        }

    }

}

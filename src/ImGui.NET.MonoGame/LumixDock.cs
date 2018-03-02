// Lumix Engine docking system for dear imgui
// Used under the MIT License.

/*
The MIT License (MIT)

Copyright (c) 2013-2017 Mikulas Florek

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
//using System.Numerics;
using static ImGuiNET.ImGuiNative;

namespace ImGuiDock
{
    public unsafe class DockContext
    {
        private enum Slot
        {
            Left,
            Right,
            Top,
            Bottom,
            Tab,

            Float,
            None
        };

        private enum EndAction
        {
            None,
            Panel,
            End,
            EndChild
        };

        public enum Status
        {
            Docked,
            Float,
            Dragged
        };

        public class CharArray16
        {
            public byte[] Values = new byte[16];

            public byte Get(int index)
            {
                fixed (byte* valuesPtr = Values)
                {
                    return valuesPtr[index];
                }
            }

            public void Set(int index, byte value)
            {
                fixed (byte* valuesPtr = Values)
                {
                    valuesPtr[index] = value;
                }
            }

            public byte this[int index]
            {
                get => Get(index);
                set => Set(index, value);
            }
        }

        public class Dock
        {
            public string label;
            public uint id;
            public Dock next_tab;
            public Dock prev_tab;
            public Dock[] children = new Dock[2];
            public Dock parent;
            public bool active;
            public Vector2 pos;
            public Vector2 size;
            public Status status;
            public CharArray16 location = new CharArray16();
            public bool opened;
            public bool first;
            public int last_frame;

            public static implicit operator bool(Dock d) => d != null;

            public Dock()
            {
                size = new Vector2(-1, -1);
                active = true;
                status = Status.Float;
                first = true;
            }

            public Vector2 getMinSize()
            {
                if (children[0] == null) return new Vector2(16, 16 + igGetTextLineHeightWithSpacing());

                Vector2 s0 = children[0].getMinSize();
                if (children[1] == null) { return s0; }
                else
                {
                    Vector2 s1 = children[1].getMinSize();
                    return isHorizontal() ? new Vector2(s0.X + s1.X, Math.Max(s0.Y, s1.Y))
                                          : new Vector2(Math.Max(s0.X, s1.X), s0.Y + s1.Y);
                }
            }

            public bool isHorizontal() { return children[1] == null || children[0].pos.X < children[1].pos.X; }


            public void setParent(Dock dock)
            {
                parent = dock;
                for (Dock tmp = prev_tab; tmp; tmp = tmp.prev_tab) tmp.parent = dock;
                for (Dock tmp = next_tab; tmp; tmp = tmp.next_tab) tmp.parent = dock;
            }


            public Dock getSibling()
            {
                Debug.Assert(parent != null);
                if (parent.children[0] == getFirstTab()) return parent.children[1];
                return parent.children[0];
            }

            public Dock getFirstTab()
            {
                Dock tmp = this;
                while (tmp.prev_tab) tmp = tmp.prev_tab;
                return tmp;
            }


            public void setActive()
            {
                active = true;
                for (Dock tmp = prev_tab; tmp; tmp = tmp.prev_tab) tmp.active = false;
                for (Dock tmp = next_tab; tmp; tmp = tmp.next_tab) tmp.active = false;
            }


            public bool hasChildren() { return children[0] != null; }


            void setChildrenPosSize(Vector2 _pos, Vector2 _size)
            {
                Vector2 s = children[0].size;
                if (isHorizontal())
                {
                    s.Y = _size.Y;
                    s.X = (float)(int)(
                          _size.X * children[0].size.X / (children[0].size.X + children[1].size.X));
                    if (s.X < children[0].getMinSize().X)
                    {
                        s.X = children[0].getMinSize().X;
                    }
                    else if (_size.X - s.X < children[1].getMinSize().X)
                    {
                        s.X = _size.X - children[1].getMinSize().X;
                    }
                    children[0].setPosSize(_pos, s);

                    s.X = _size.X - children[0].size.X;
                    Vector2 p = _pos;
                    p.X += children[0].size.X;
                    children[1].setPosSize(p, s);
                }
                else
                {
                    s.X = _size.X;
                    s.Y = (float)(int)(
                          _size.Y * children[0].size.Y / (children[0].size.Y + children[1].size.Y));
                    if (s.Y < children[0].getMinSize().Y)
                    {
                        s.Y = children[0].getMinSize().Y;
                    }
                    else if (_size.Y - s.Y < children[1].getMinSize().Y)
                    {
                        s.Y = _size.Y - children[1].getMinSize().Y;
                    }
                    children[0].setPosSize(_pos, s);

                    s.Y = _size.Y - children[0].size.Y;
                    Vector2 p = _pos;
                    p.Y += children[0].size.Y;
                    children[1].setPosSize(p, s);
                }
            }


            public void setPosSize(Vector2 _pos, Vector2 _size)
            {
                size = _size;
                pos = _pos;
                for (Dock tmp = prev_tab; tmp; tmp = tmp.prev_tab)
                {
                    tmp.size = _size;
                    tmp.pos = _pos;
                }
                for (Dock tmp = next_tab; tmp; tmp = tmp.next_tab)
                {
                    tmp.size = _size;
                    tmp.pos = _pos;
                }

                if (!hasChildren()) return;
                setChildrenPosSize(_pos, _size);
            }
        }

        List<Dock> m_docks = new List<Dock>();
        Vector2 m_drag_offset;
        Dock m_current = null;
        int m_last_frame = 0;
        EndAction m_end_action;
        bool m_is_begin_open = false;

        Dock getDock(string label, bool opened, Vector2 default_size)
        {
            uint id = (uint)label.GetHashCode();
            for (int i = 0; i < m_docks.Count; ++i)
            {
                if (m_docks[i].id == id) return m_docks[i];
            }

            Dock new_dock = new Dock();
            m_docks.Add(new_dock);
            new_dock.label = label;
            Debug.Assert(new_dock.label != null);
            new_dock.id = id;
            new_dock.setActive();
            new_dock.status = Status.Float;
            new_dock.pos = new Vector2(0, 0);
            new_dock.size.X = default_size.X < 0 ? igGetIO()->DisplaySize.X : default_size.X;
            new_dock.size.Y = default_size.Y < 0 ? igGetIO()->DisplaySize.Y : default_size.Y;
            new_dock.opened = opened;
            new_dock.first = true;
            new_dock.location[0] = 0;
            return new_dock;
        }

        void putInBackground()
        {
            NativeWindow* win = GetCurrentWindow();
            NativeContext* g = igGetCurrentContext();
            if (((NativeWindow**)g->Windows.Data)[0] == win) return;

            for (int i = 0; i < g->Windows.Size; i++)
            {
                if (((NativeWindow**)g->Windows.Data)[i] == win)
                {
                    for (int j = i - 1; j >= 0; --j)
                    {
                        ((NativeWindow**)g->Windows.Data)[j + 1] = ((NativeWindow**)g->Windows.Data)[j];
                    }
                    ((NativeWindow**)g->Windows.Data)[0] = win;
                    break;
                }
            }
        }


        void splits()
        {
            if (igGetFrameCount() == m_last_frame) return;
            m_last_frame = igGetFrameCount();

            putInBackground();

            uint color = igGetColorU32(ColorTarget.Button, 1);
            uint color_hovered = igGetColorU32(ColorTarget.ButtonHovered, 1);
            NativeDrawList* draw_list = igGetWindowDrawList();
            NativeIO* io = igGetIO();
            for (int i = 0; i < m_docks.Count; ++i)
            {
                Dock dock = m_docks[i];
                if (!dock.hasChildren()) continue;

                igPushIDInt(i);
                if (!igIsMouseDown(0)) dock.status = Status.Docked;

                Vector2 size = dock.children[0].size;
                Vector2 dsize = new Vector2(0, 0);
                igSetCursorScreenPos(dock.children[1].pos);
                Vector2 min_size0 = dock.children[0].getMinSize();
                Vector2 min_size1 = dock.children[1].getMinSize();
                if (dock.isHorizontal())
                {
                    igInvisibleButton("split", new Vector2(3, dock.size.Y));
                    if (dock.status == Status.Dragged) dsize.X = io->MouseDelta.X;
                    dsize.X = -Math.Min(-dsize.X, dock.children[0].size.X - min_size0.X);
                    dsize.X = Math.Min(dsize.X, dock.children[1].size.X - min_size1.X);
                }
                else
                {
                    igInvisibleButton("split", new Vector2(dock.size.X, 3));
                    if (dock.status == Status.Dragged) dsize.Y = io->MouseDelta.Y;
                    dsize.Y = -Math.Min(-dsize.Y, dock.children[0].size.Y - min_size0.Y);
                    dsize.Y = Math.Min(dsize.Y, dock.children[1].size.Y - min_size1.Y);
                }
                Vector2 new_size0 = dock.children[0].size + dsize;
                Vector2 new_size1 = dock.children[1].size - dsize;
                Vector2 new_pos1 = dock.children[1].pos + dsize;
                dock.children[0].setPosSize(dock.children[0].pos, new_size0);
                dock.children[1].setPosSize(new_pos1, new_size1);

                if (igIsItemHovered(HoveredFlags.Default) && igIsMouseClicked(0, false))
                {
                    dock.status = Status.Dragged;
                }

                igGetItemRectMin(out Vector2 min);
                igGetItemRectMax(out Vector2 max);
                new DrawList(draw_list).AddRectFilled(
                    min, max, igIsItemHovered(HoveredFlags.Default) ? color_hovered : color, 0f);
                igPopID();
            }
        }


        void beginPanel()
        {
            WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoResize | WindowFlags.NoMove | WindowFlags.NoCollapse
                | WindowFlags.NoSavedSettings | WindowFlags.NoScrollbar | WindowFlags.NoScrollWithMouse | WindowFlags.NoBringToFrontOnFocus;
            Dock root = getRootDock();
            if (root)
            {
                igSetNextWindowPos(root.pos, 0, new Vector2());
                igSetNextWindowSize(root.size, 0);
            }
            else
            {
                igSetNextWindowPos(new Vector2(0, 0), 0, new Vector2());
                igSetNextWindowSize(igGetIO()->DisplaySize, 0);
            }
            igPushStyleVar(StyleVar.WindowRounding, 0);
            bool opened = true;
            igBegin("###DockPanel", ref opened, flags);
            splits();
        }


        void endPanel()
        {
            igEnd();
            igPopStyleVar(1);
        }


        Dock getDockAt(Vector2 pos)
        {
            for (int i = 0; i < m_docks.Count; ++i)
            {
                Dock dock = m_docks[i];
                if (dock.hasChildren()) continue;
                if (dock.status != Status.Docked) continue;
                if (igIsMouseHoveringRect(dock.pos, dock.pos + dock.size, false))
                {
                    return dock;
                }
            }

            return null;
        }

        static ImRect getDockedRect(ImRect rect, Slot dock_slot)
        {
            Vector2 half_size = rect.GetSize() * 0.5f;
            switch (dock_slot)
            {
                default: return rect;
                case Slot.Top: return new ImRect(rect.Min, rect.Min + new Vector2(rect.Max.X, half_size.Y));
                case Slot.Right: return new ImRect(rect.Min + new Vector2(half_size.X, 0), rect.Max);
                case Slot.Bottom: return new ImRect(rect.Min + new Vector2(0, half_size.Y), rect.Max);
                case Slot.Left: return new ImRect(rect.Min, rect.Min + new Vector2(half_size.X, rect.Max.Y));
            }
        }


        static ImRect getSlotRect(ImRect parent_rect, Slot dock_slot)
        {
            Vector2 size = parent_rect.Max - parent_rect.Min;
            Vector2 center = parent_rect.Min + size * 0.5f;
            switch (dock_slot)
            {
                default: return new ImRect(center - new Vector2(20, 20), center + new Vector2(20, 20));
                case Slot.Top: return new ImRect(center + new Vector2(-20, -50), center + new Vector2(20, -30));
                case Slot.Right: return new ImRect(center + new Vector2(30, -20), center + new Vector2(50, 20));
                case Slot.Bottom: return new ImRect(center + new Vector2(-20, +30), center + new Vector2(20, 50));
                case Slot.Left: return new ImRect(center + new Vector2(-50, -20), center + new Vector2(-30, 20));
            }
        }


        static ImRect getSlotRectOnBorder(ImRect parent_rect, Slot dock_slot)
        {
            Vector2 size = parent_rect.Max - parent_rect.Min;
            Vector2 center = parent_rect.Min + size * 0.5f;
            switch (dock_slot)
            {
                case Slot.Top:
                    return new ImRect(new Vector2(center.X - 20, parent_rect.Min.Y + 10),
                        new Vector2(center.X + 20, parent_rect.Min.Y + 30));
                case Slot.Left:
                    return new ImRect(new Vector2(parent_rect.Min.X + 10, center.Y - 20),
                        new Vector2(parent_rect.Min.X + 30, center.Y + 20));
                case Slot.Bottom:
                    return new ImRect(new Vector2(center.X - 20, parent_rect.Max.Y - 30),
                        new Vector2(center.X + 20, parent_rect.Max.Y - 10));
                case Slot.Right:
                    return new ImRect(new Vector2(parent_rect.Max.X - 30, center.Y - 20),
                        new Vector2(parent_rect.Max.X - 10, center.Y + 20));
                default: throw new InvalidOperationException();
            }

            throw new InvalidOperationException();
        }


        Dock getRootDock()
        {
            for (int i = 0; i < m_docks.Count; ++i)
            {
                if (!m_docks[i].parent &&
                    (m_docks[i].status == Status.Docked || m_docks[i].children[0]))
                {
                    return m_docks[i];
                }
            }
            return null;
        }


        bool dockSlots(Dock dock, Dock dest_dock, ImRect rect, bool on_border)
        {
            var canvas = new DrawList(igGetWindowDrawList());
            uint color = igGetColorU32(ColorTarget.Button, 1f);
            uint color_hovered = igGetColorU32(ColorTarget.ButtonHovered, 1f);
            Vector2 mouse_pos = igGetIO()->MousePos;
            for (int i = 0; i < (on_border ? 4 : 5); ++i)
            {
                ImRect r =
                    on_border ? getSlotRectOnBorder(rect, (Slot)i) : getSlotRect(rect, (Slot)i);
                bool hovered = r.Contains(mouse_pos);
                canvas.AddRectFilled(r.Min, r.Max, hovered ? color_hovered : color, 0f);
                if (!hovered) continue;

                if (!igIsMouseDown(0))
                {
                    doDock(dock, dest_dock ? dest_dock : getRootDock(), (Slot)i);
                    return true;
                }
                ImRect docked_rect = getDockedRect(rect, (Slot)i);
                canvas.AddRectFilled(docked_rect.Min, docked_rect.Max, igGetColorU32(ColorTarget.Button, 1f), 0f);
            }
            return false;
        }


        void handleDrag(Dock dock)
        {
            Dock dest_dock = getDockAt(igGetIO()->MousePos);

            bool opened = true;
            igBegin("##Overlay",
                ref opened,
                /*NativeWindowFlags_Tooltip | */ WindowFlags.NoTitleBar | WindowFlags.NoMove |
                     WindowFlags.NoResize | WindowFlags.NoSavedSettings | WindowFlags.AlwaysAutoResize);
            DrawList canvas = new DrawList(igGetWindowDrawList());

            canvas.PushClipRectFullScreen();

            uint docked_color = igGetColorU32(ColorTarget.FrameBg, 1f);
            docked_color = (docked_color & 0x00ffFFFF) | 0x80000000;
            dock.pos = igGetIO()->MousePos - m_drag_offset;
            if (dest_dock)
            {
                if (dockSlots(dock,
                        dest_dock,
                        new ImRect(dest_dock.pos, dest_dock.pos + dest_dock.size),
                        false))
                {
                    canvas.PopClipRect();
                    igEnd();
                    return;
                }
            }
            if (dockSlots(dock, null, new ImRect(new Vector2(0, 0), igGetIO()->DisplaySize), true))
            {
                canvas.PopClipRect();
                igEnd();
                return;
            }
            canvas.AddRectFilled(dock.pos, dock.pos + dock.size, docked_color, 0f);
            canvas.PopClipRect();

            if (!igIsMouseDown(0))
            {
                dock.status = Status.Float;
                dock.location[0] = 0;
                dock.setActive();
            }

            igEnd();
        }


        void fillLocation(Dock dock)
        {
            if (dock.status == Status.Float) return;
            CharArray16 c = dock.location;
            Dock tmp = dock;
            int i = 0;
            while (tmp.parent)
            {
                c.Set(i, getLocationCode(tmp));
                tmp = tmp.parent;
                i += 1;
            }

            c.Set(i, 0);
        }

        void doUndock(Dock dock)
        {
            if (dock.prev_tab)
                dock.prev_tab.setActive();
            else if (dock.next_tab)
                dock.next_tab.setActive();
            else
                dock.active = false;
            Dock container = dock.parent;

            if (container)
            {
                Dock sibling = dock.getSibling();
                if (container.children[0] == dock)
                {
                    container.children[0] = dock.next_tab;
                }
                else if (container.children[1] == dock)
                {
                    container.children[1] = dock.next_tab;
                }

                bool remove_container = !container.children[0] || !container.children[1];
                if (remove_container)
                {
                    if (container.parent)
                    {
                        int index = container.parent.children[0] == container
                                           ? 0
                                           : 1;
                        ref Dock child = ref container.parent.children[index];
                        child = sibling;
                        child.setPosSize(container.pos, container.size);
                        child.setParent(container.parent);
                    }
                    else
                    {
                        if (container.children[0])
                        {
                            container.children[0].setParent(null);
                            container.children[0].setPosSize(container.pos, container.size);
                        }
                        if (container.children[1])
                        {
                            container.children[1].setParent(null);
                            container.children[1].setPosSize(container.pos, container.size);
                        }
                    }

                    bool removed = false;
                    for (int i = 0; i < m_docks.Count; ++i)
                    {
                        if (m_docks[i] == container)
                        {
                            removed = true;
                            m_docks.RemoveAt(i);
                            break;
                        }

                    }
                    Debug.Assert(removed);

                    //container.~Dock();
                    //MemFree(container);
                }
            }
            if (dock.prev_tab) dock.prev_tab.next_tab = dock.next_tab;
            if (dock.next_tab) dock.next_tab.prev_tab = dock.prev_tab;
            dock.parent = null;
            dock.prev_tab = dock.next_tab = null;
        }


        void drawTabbarListButton(Dock dock)
        {
            if (!dock.next_tab) return;

            var nativeDrawList = igGetWindowDrawList();
            var draw_list = new DrawList(nativeDrawList);
            if (igInvisibleButton("list", new Vector2(16, 16)))
            {
                igOpenPopup("tab_list_popup");
            }
            if (igBeginPopup("tab_list_popup"))
            {
                Dock tmp = dock;
                while (tmp)
                {
                    bool dummy = false;
                    if (igSelectableEx(tmp.label, ref dummy, SelectableFlags.Default, new Vector2()))
                    {
                        tmp.setActive();
                    }
                    tmp = tmp.next_tab;
                }
                igEndPopup();
            }

            bool hovered = igIsItemHovered(HoveredFlags.Default);
            igGetItemRectMin(out Vector2 min);
            igGetItemRectMax(out Vector2 max);
            Vector2 center = (min + max) * 0.5f;
            uint text_color = igGetColorU32(ColorTarget.Text, 1f);
            uint color_active = igGetColorU32(ColorTarget.FrameBgActive, 1f);
            draw_list.AddRectFilled(new Vector2(center.X - 4, min.Y + 3),
                new Vector2(center.X + 4, min.Y + 5),
                hovered ? color_active : text_color,
                0f);
            ImDrawList_AddTriangleFilled(nativeDrawList,
                new Vector2(center.X - 4, min.Y + 7),
                new Vector2(center.X + 4, min.Y + 7),
                new Vector2(center.X, min.Y + 12),
                hovered ? color_active : text_color);
        }


        bool tabbar(Dock dock, bool close_button)
        {
            float tabbar_height = 2 * igGetTextLineHeightWithSpacing();
            Vector2 size = new Vector2(dock.size.X, tabbar_height);
            bool tab_closed = false;

            igSetCursorScreenPos(dock.pos);
            string tmp = string.Format("tabs{0}", dock.id);

            if (igBeginChild(tmp, size, true, WindowFlags.Default))
            {
                Dock dock_tab = dock;

                var nativeDrawList = igGetWindowDrawList();
                var draw_list = new DrawList(nativeDrawList);
                uint color = igGetColorU32(ColorTarget.FrameBg, 1f);
                uint color_active = igGetColorU32(ColorTarget.FrameBgActive, 1f);
                uint color_hovered = igGetColorU32(ColorTarget.FrameBgHovered, 1f);
                uint button_hovered = igGetColorU32(ColorTarget.ButtonHovered, 1f);
                uint text_color = igGetColorU32(ColorTarget.Text, 1f);
                float line_height = igGetTextLineHeightWithSpacing();
                float tab_base = 0f;

                drawTabbarListButton(dock);

                while (dock_tab)
                {
                    igSameLine(0, 15);

                    int renderedTextLength = FindRenderedTextLength(dock_tab.label);
                    fixed (char* labelPtr = dock_tab.label)
                    {
                        igCalcTextSize(out Vector2 sizeX, labelPtr, labelPtr + renderedTextLength, false, -1f);
                        size = new Vector2(sizeX.X, line_height);
                    }

                    if (igInvisibleButton(dock_tab.label, size))
                    {
                        dock_tab.setActive();
                    }

                    if (igIsItemActive() && igIsMouseDragging(0, -1f))
                    {
                        m_drag_offset = ImGui.GetMousePos() - dock_tab.pos;
                        doUndock(dock_tab);
                        dock_tab.status = Status.Dragged;
                    }

                    if (dock_tab.active && close_button) size.X += 16 + igGetStyle()->ItemSpacing.X;

                    bool hovered = igIsItemHovered(HoveredFlags.Default);
                    igGetItemRectMin(out Vector2 pos);
                    tab_base = pos.Y;

                    ImDrawList_PathClear(nativeDrawList);
                    ImDrawList_PathLineTo(nativeDrawList, pos + new Vector2(-15, size.Y));
                    ImDrawList_PathBezierCurveTo(nativeDrawList, pos + new Vector2(-10, size.Y), pos + new Vector2(-5, 0), pos + new Vector2(0, 0), 10);
                    ImDrawList_PathLineTo(nativeDrawList, pos + new Vector2(size.X, 0));
                    ImDrawList_PathBezierCurveTo(nativeDrawList, pos + new Vector2(size.X + 5, 0),
                        pos + new Vector2(size.X + 10, size.Y),
                        pos + new Vector2(size.X + 15, size.Y),
                        10);
                    ImDrawList_PathFillConvex(nativeDrawList,
                        hovered ? color_hovered : (dock_tab.active ? color_active : color));
                    draw_list.AddText(pos, dock_tab.label, text_color);
                    //ImDrawList_AddText(nativeDrawList,
                    //    pos, text_color, dock_tab.label, text_end);

                    if (dock_tab.active && close_button)
                    {
                        igSameLine(0, -1f);
                        tab_closed = igInvisibleButton("close", new Vector2(16, 16));
                        Vector2 center = (GetItemRectMin() + GetItemRectMax()) * 0.5f;
                        if (igIsItemHovered(HoveredFlags.Default))
                        {
                            draw_list.AddRectFilled(center + new Vector2(-6.0f, -6.0f), center + new Vector2(7.0f, 7.0f), button_hovered, 0f);
                        }
                        draw_list.AddLine(
                            center + new Vector2(-3.5f, -3.5f), center + new Vector2(3.5f, 3.5f), text_color, 1f);
                        draw_list.AddLine(
                            center + new Vector2(3.5f, -3.5f), center + new Vector2(-3.5f, 3.5f), text_color, 1f);
                    }

                    dock_tab = dock_tab.next_tab;
                }
                Vector2 cp = new Vector2(dock.pos.X, tab_base + line_height);
                draw_list.AddLine(cp, cp + new Vector2(dock.size.X, 0), color, 1f);
            }
            igEndChild();
            return tab_closed;
        }

        private Vector2 GetItemRectMin()
        {
            igGetItemRectMin(out Vector2 ret);
            return ret;
        }

        private Vector2 GetItemRectMax()
        {
            igGetItemRectMax(out Vector2 ret);
            return ret;
        }

        private int FindRenderedTextLength(string text)
        {
            int text_display_length = 0;
            while (text_display_length < text.Length && text[text_display_length] != 0)
            {
                if (text[text_display_length] == '#' &&
                    (text.Length >= (text_display_length + 1) && text[text_display_length + 1] == '#'))
                {
                    break;
                }

                text_display_length += 1;
            }

            return text_display_length;
        }

        static void setDockPosSize(Dock dest, Dock dock, Slot dock_slot, Dock container)
        {
            Debug.Assert(!dock.prev_tab && !dock.next_tab && !dock.children[0] && !dock.children[1]);

            dest.pos = container.pos;
            dest.size = container.size;
            dock.pos = container.pos;
            dock.size = container.size;

            switch (dock_slot)
            {
                case Slot.Bottom:
                    dest.size.Y *= 0.5f;
                    dock.size.Y *= 0.5f;
                    dock.pos.Y += dest.size.Y;
                    break;
                case Slot.Right:
                    dest.size.X *= 0.5f;
                    dock.size.X *= 0.5f;
                    dock.pos.X += dest.size.X;
                    break;
                case Slot.Left:
                    dest.size.X *= 0.5f;
                    dock.size.X *= 0.5f;
                    dest.pos.X += dock.size.X;
                    break;
                case Slot.Top:
                    dest.size.Y *= 0.5f;
                    dock.size.Y *= 0.5f;
                    dest.pos.Y += dock.size.Y;
                    break;
                default: throw new InvalidOperationException();
            }
            dest.setPosSize(dest.pos, dest.size);

            if (container.children[1].pos.X < container.children[0].pos.X ||
                container.children[1].pos.Y < container.children[0].pos.Y)
            {
                Dock tmp = container.children[0];
                container.children[0] = container.children[1];
                container.children[1] = tmp;
            }
        }


        void doDock(Dock dock, Dock dest, Slot dock_slot)
        {
            Debug.Assert(!dock.parent);
            if (!dest)
            {
                dock.status = Status.Docked;
                dock.setPosSize(new Vector2(0, 0), igGetIO()->DisplaySize);
            }
            else if (dock_slot == Slot.Tab)
            {
                Dock tmp = dest;
                while (tmp.next_tab)
                {
                    tmp = tmp.next_tab;
                }

                tmp.next_tab = dock;
                dock.prev_tab = tmp;
                dock.size = tmp.size;
                dock.pos = tmp.pos;
                dock.parent = dest.parent;
                dock.status = Status.Docked;
            }
            else if (dock_slot == Slot.None)
            {
                dock.status = Status.Float;
            }
            else
            {
                Dock container = new Dock();
                m_docks.Add(container);
                container.children[0] = dest.getFirstTab();
                container.children[1] = dock;
                container.next_tab = null;
                container.prev_tab = null;
                container.parent = dest.parent;
                container.size = dest.size;
                container.pos = dest.pos;
                container.status = Status.Docked;
                container.label = string.Empty;

                if (!dest.parent)
                {
                }
                else if (dest.getFirstTab() == dest.parent.children[0])
                {
                    dest.parent.children[0] = container;
                }
                else
                {
                    dest.parent.children[1] = container;
                }

                dest.setParent(container);
                dock.parent = container;
                dock.status = Status.Docked;

                setDockPosSize(dest, dock, dock_slot, container);
            }
            dock.setActive();
        }

        static bool is_first_call = true;

        public void rootDock(Vector2 pos, Vector2 size)
        {
            Dock root = getRootDock();
            if (!root) return;

            Vector2 min_size = root.getMinSize();
            Vector2 requested_size = size;
            root.setPosSize(pos, Vector2.Max(min_size, requested_size));

            if (!is_first_call)
            {
                for (int i = 0; i < m_docks.Count; i++)
                {
                    Dock dock = m_docks[i];
                    if (!dock.hasChildren() && dock != root && (igGetFrameCount() - dock.last_frame) > 1)
                    {
                        doUndock(dock);
                        int newIndex = m_docks.IndexOf(dock);
                        m_docks.RemoveAt(newIndex);
                        i = newIndex;
                    }
                }
            }
            is_first_call = false;
        }


        public void setDockActive()
        {
            Debug.Assert(m_current);
            if (m_current) m_current.setActive();
        }

        private Vector2 GetCurrentSize()
        {
            Debug.Assert(m_current);
            return m_current.size;
        }

        private Vector2 GetCurrentPosition()
        {
            Debug.Assert(m_current);
            return m_current.pos;
        }

        static Slot getSlotFromLocationCode(byte code)
        {
            switch (code)
            {
                case (byte)'1': return Slot.Left;
                case (byte)'2': return Slot.Top;
                case (byte)'3': return Slot.Bottom;
                default: return Slot.Right;
            }
        }


        static byte getLocationCode(Dock dock)
        {
            if (!dock) return (byte)'0';

            if (dock.parent.isHorizontal())
            {
                if (dock.pos.X < dock.parent.children[0].pos.X) return (byte)'1';
                if (dock.pos.X < dock.parent.children[1].pos.X) return (byte)'1';
                return (byte)'0';
            }
            else
            {
                if (dock.pos.Y < dock.parent.children[0].pos.Y) return (byte)'2';
                if (dock.pos.Y < dock.parent.children[1].pos.Y) return (byte)'2';
                return (byte)'3';
            }
        }


        void tryDockToStoredLocation(Dock dock)
        {
            if (dock.status == Status.Docked) return;
            if (dock.location[0] == 0) return;

            Dock tmp = getRootDock();
            if (!tmp) return;

            Dock prev = null;
            fixed (byte* loc = dock.location.Values)
            {
                byte* c = loc + strlen(loc) - 1;
                while (c >= loc && tmp)
                {
                    prev = tmp;
                    tmp = *c == getLocationCode(tmp.children[0]) ? tmp.children[0] : tmp.children[1];
                    if (tmp) --c;
                }
                if (tmp && tmp.children[0]) tmp = tmp.parent;
                doDock(dock, tmp ? tmp : prev, tmp && !tmp.children[0] ? Slot.Tab : getSlotFromLocationCode(*c));
            }
        }

        private int strlen(byte* loc)
        {
            int ret = 0;
            while (loc[ret] != 0)
            {
                ret++;
            }

            return ret;
        }

        void cleanDocks()
        {
            restart:
            for (int i = 0, c = m_docks.Count; i < c; ++i)
            {
                Dock dock = m_docks[i];
                if (dock.last_frame == 0 && dock.status != Status.Float && !dock.children[0])
                {
                    fillLocation(m_docks[i]);
                    doUndock(m_docks[i]);
                    m_docks[i].status = Status.Float;
                    goto restart;
                }
            }
        }

        bool begin(string label, bool* opened, WindowFlags extra_flags, Vector2 default_size)
        {
            Debug.Assert(!m_is_begin_open);
            m_is_begin_open = true;
            Dock dock = getDock(label, opened == null || *opened, default_size);
            if (dock.last_frame != 0 && m_last_frame != igGetFrameCount())
            {
                cleanDocks();
            }
            dock.last_frame = igGetFrameCount();
            if (!dock.opened && (opened == null || *opened)) tryDockToStoredLocation(dock);
            if (dock.label != label)
            {
                dock.label = label;
            }

            m_end_action = EndAction.None;

            if (dock.first && opened != null) *opened = dock.opened;
            dock.first = false;
            if (opened != null && !*opened)
            {
                if (dock.status != Status.Float)
                {
                    fillLocation(dock);
                    doUndock(dock);
                    dock.status = Status.Float;
                }
                dock.opened = false;
                return false;
            }
            dock.opened = true;

            m_end_action = EndAction.Panel;
            beginPanel();

            m_current = dock;
            if (dock.status == Status.Dragged) handleDrag(dock);

            bool is_float = dock.status == Status.Float;

            if (is_float)
            {
                igSetNextWindowPos(dock.pos, 0, new Vector2());
                igSetNextWindowSize(dock.size, Condition.FirstUseEver);
                bool opened2 = opened != null ? *opened : false;
                bool ret1 = igBegin(label,
                    ref opened2,
                     WindowFlags.NoCollapse | extra_flags);
                if (opened != null) *opened = opened2;
                m_end_action = EndAction.End;
                dock.pos = ImGui.GetWindowPosition();
                dock.size = ImGui.GetWindowSize();

                NativeContext* g = igGetCurrentContext();

                if (g->ActiveId == GetCurrentWindow()->MoveId && g->IO.MouseDown[0] == 1)
                {
                    m_drag_offset = ImGui.GetMousePos() - dock.pos;
                    doUndock(dock);
                    dock.status = Status.Dragged;
                }
                return ret1;
            }

            if (!dock.active && dock.status != Status.Dragged) return false;

            m_end_action = EndAction.EndChild;

            igPushStyleColor(ColorTarget.Border, new Vector4(0, 0, 0, 0));
            igPushStyleColor(ColorTarget.BorderShadow, new Vector4(0, 0, 0, 0));
            float tabbar_height = igGetTextLineHeightWithSpacing();
            if (tabbar(dock.getFirstTab(), opened != null))
            {
                fillLocation(dock);
                *opened = false;
            }
            Vector2 pos = dock.pos;
            Vector2 size = dock.size;
            pos.Y += tabbar_height + igGetStyle()->WindowPadding.Y;
            size.Y -= tabbar_height + igGetStyle()->WindowPadding.Y;

            igSetCursorScreenPos(pos);
            WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoResize |
                                     WindowFlags.NoMove | WindowFlags.NoCollapse |
                                      WindowFlags.NoSavedSettings | WindowFlags.NoBringToFrontOnFocus |
                                     extra_flags;
            string tmp = label + "_docked";
            bool ret = igBeginChild(tmp, size, true, flags);
            igPopStyleColor(1);
            igPopStyleColor(1);
            return ret;
        }

        private NativeWindow* GetCurrentWindow()
        {
            NativeContext* g = igGetCurrentContext();
            g->CurrentWindow->WriteAccessed = 1;
            return g->CurrentWindow;
        }

        void end()
        {
            if (m_end_action == EndAction.End)
            {
                igEnd();
            }
            else if (m_end_action == EndAction.EndChild)
            {
                igPushStyleColor(ColorTarget.Border, new Vector4(0, 0, 0, 0));
                igPushStyleColor(ColorTarget.BorderShadow, new Vector4(0, 0, 0, 0));
                igEndChild();
                igPopStyleColor(1);
                igPopStyleColor(1);
            }
            m_current = null;
            if (m_end_action > EndAction.None) endPanel();
            m_is_begin_open = false;
        }


        int GetDockIndex(Dock dock)
        {
            if (!dock) return -1;

            for (int i = 0; i < m_docks.Count; ++i)
            {
                if (dock == m_docks[i]) return i;
            }

            Debug.Assert(false);
            return -1;
        }

        private Dock GetDockByIndex(int index)
        {
            return index < 0 ? null : m_docks[index];
        }

        private static readonly DockContext g_dock = new DockContext();

        public static void RootDock(Vector2 pos, Vector2 size)
        {
            g_dock.rootDock(pos, size);
        }

        public static void SetDockActive()
        {
            g_dock.setDockActive();
        }

        public static Vector2 GetCurrentDockPosition()
        {
            return g_dock.GetCurrentPosition();
        }

        public static Vector2 GetCurrentDockSize()
        {
            return g_dock.GetCurrentSize();
        }

        public static bool BeginDock(string label, WindowFlags extra_flags, Vector2 default_size)
            => BeginDock(label, null, extra_flags, default_size);

        public static bool BeginDock(string label, ref bool opened, WindowFlags extra_flags, Vector2 default_size)
        {
            bool opened_local = opened;
            bool ret = BeginDock(label, &opened_local, extra_flags, default_size);
            opened = opened_local;
            return ret;
        }

        public static bool BeginDock(string label, bool* opened, WindowFlags extra_flags, Vector2 default_size)
        {
            return g_dock.begin(label, opened, extra_flags, default_size);
        }

        public static void EndDock()
        {
            g_dock.end();
        }
    }
} // namespace ImGui
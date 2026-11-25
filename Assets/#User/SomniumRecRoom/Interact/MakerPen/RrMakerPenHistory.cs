using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// Undo / redo of the local player's own maker-room edits, Rec Room's 10 steps. Each step is one object's
    /// state before and after (a create has no before, a delete no after), applied back through
    /// <see cref="IRrMakerRoomHost"/>: undoing a create deletes the object, undoing a delete creates it again with
    /// its old id (if the room gives it a new one, every step that names the old id is moved to the new one), and
    /// undoing a change writes the old state back. A step whose object someone else deleted meanwhile is dropped.
    /// The history belongs to the pen and goes with it; config edits (physics, freeze) are undoable here too.
    /// </summary>
    public sealed class RrMakerPenHistory
    {
        public const int Steps = 10;

        private sealed class Step
        {
            public uint Id;
            public string What;
            public RrMakerObject Before;
            public RrMakerObject After;
        }

        private readonly List<Step> _undo = new List<Step>();
        private readonly List<Step> _redo = new List<Step>();

        public int UndoCount
        {
            get { return _undo.Count; }
        }

        public int RedoCount
        {
            get { return _redo.Count; }
        }

        public void Created(RrMakerObject after, string what)
        {
            if (after != null)
            {
                Push(new Step { Id = after.Id, What = what, After = after.Clone() });
            }
        }

        public void Changed(RrMakerObject before, RrMakerObject after, string what)
        {
            if (before != null && after != null)
            {
                Push(new Step { Id = after.Id, What = what, Before = before.Clone(), After = after.Clone() });
            }
        }

        public void Deleted(RrMakerObject before, string what)
        {
            if (before != null)
            {
                Push(new Step { Id = before.Id, What = what, Before = before.Clone() });
            }
        }

        /// <summary>The newest step about this object gets a new "after" (a placement corrected once the object was built).</summary>
        public void Amend(uint id, RrMakerObject after)
        {
            for (int i = _undo.Count - 1; i >= 0; i--)
            {
                if (_undo[i].Id == id)
                {
                    if (_undo[i].After != null)
                    {
                        _undo[i].After = after.Clone();
                    }

                    return;
                }
            }
        }

        /// <summary>Undoes the newest step; returns what was undone ("" when nothing could be).</summary>
        public string Undo(IRrMakerRoomHost host)
        {
            while (_undo.Count > 0)
            {
                var s = _undo[_undo.Count - 1];
                _undo.RemoveAt(_undo.Count - 1);
                if (Apply(host, s, s.Before))
                {
                    _redo.Add(s);
                    return s.What;
                }
            }

            return "";
        }

        /// <summary>Redoes the newest undone step; returns what was redone ("" when nothing could be).</summary>
        public string Redo(IRrMakerRoomHost host)
        {
            while (_redo.Count > 0)
            {
                var s = _redo[_redo.Count - 1];
                _redo.RemoveAt(_redo.Count - 1);
                if (Apply(host, s, s.After))
                {
                    _undo.Add(s);
                    Trim();
                    return s.What;
                }
            }

            return "";
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        private void Push(Step s)
        {
            _undo.Add(s);
            _redo.Clear();
            Trim();
        }

        private void Trim()
        {
            while (_undo.Count > Steps)
            {
                _undo.RemoveAt(0);
            }
        }

        /// <summary>Brings the object to <paramref name="state"/> (null = deleted); false when that cannot be done any more.</summary>
        private bool Apply(IRrMakerRoomHost host, Step s, RrMakerObject state)
        {
            if (host == null)
            {
                return false;
            }

            var now = host.Get(s.Id);
            if (state == null)
            {
                if (now == null)
                {
                    return false;
                }

                host.Delete(s.Id);
                return true;
            }

            if (now == null)
            {
                bool wasDeleted = s.Before == null || s.After == null;
                if (!wasDeleted)
                {
                    RrLog.Info("MakerPen", "undo skipped: object " + s.Id + " was deleted by someone else");
                    return false;
                }

                var o = state.Clone();
                o.Id = s.Id;
                uint id = host.Create(o);
                if (id == 0)
                {
                    return false;
                }

                if (id != s.Id)
                {
                    uint old = s.Id;
                    Remap(old, id);
                    Remap(new List<Step> { s }, old, id);
                }

                return true;
            }

            var u = state.Clone();
            u.Id = s.Id;
            host.Update(u);
            return true;
        }

        private void Remap(uint from, uint to)
        {
            Remap(_undo, from, to);
            Remap(_redo, from, to);
        }

        private static void Remap(List<Step> steps, uint from, uint to)
        {
            foreach (var s in steps)
            {
                if (s.Id != from)
                {
                    continue;
                }

                s.Id = to;
                if (s.Before != null)
                {
                    s.Before.Id = to;
                }

                if (s.After != null)
                {
                    s.After.Id = to;
                }
            }
        }
    }
}

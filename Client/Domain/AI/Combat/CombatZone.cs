using Client.Domain.Common;
using Client.Domain.ValueObjects;
using System.Collections.ObjectModel;

namespace Client.Domain.AI.Combat
{
    public enum ZoneType
    {
        Free,
        DynamicCircle,
        FixedPolygon
    }

    public class CombatZone : ObservableObject
    {
        public CombatZone()
        {
            Vertices = new ObservableCollection<Vector3>();
        }

        public ZoneType Type { get { return type; } set { if (type != value) { type = value; OnPropertyChanged(); } } }
        public Vector3 Center { get { return center; } set { if (center != value) { center = value; OnPropertyChanged(); } } }
        public float Radius { get { return radius; } set { if (radius != value) { radius = value; OnPropertyChanged(); } } }
        public bool IsRelativeToHero { get { return isRelativeToHero; } set { if (isRelativeToHero != value) { isRelativeToHero = value; OnPropertyChanged(); } } }
        public ObservableCollection<Vector3> Vertices { get; }
        public uint MaxZDelta { get { return maxZDelta; } set { if (maxZDelta != value) { maxZDelta = value; OnPropertyChanged(); } } }
        public bool BypassObstacles { get { return bypassObstacles; } set { if (bypassObstacles != value) { bypassObstacles = value; OnPropertyChanged(); } } }
        public int BypassTimeoutMs { get { return bypassTimeoutMs; } set { if (bypassTimeoutMs != value) { bypassTimeoutMs = value; OnPropertyChanged(); } } }
        public int StepBackMs { get { return stepBackMs; } set { if (stepBackMs != value) { stepBackMs = value; OnPropertyChanged(); } } }
        public int StepSideMs { get { return stepSideMs; } set { if (stepSideMs != value) { stepSideMs = value; OnPropertyChanged(); } } }

        public bool IsInside(Vector3 point, Vector3 heroPosition = null)
        {
            switch (Type)
            {
                case ZoneType.Free:
                    return true;
                case ZoneType.DynamicCircle:
                    var c = IsRelativeToHero && heroPosition != null ? heroPosition : Center;
                    return c.HorizontalDistance(point) <= Radius;
                case ZoneType.FixedPolygon:
                    return IsPointInPolygon(point);
            }
            return true;
        }

        private bool IsPointInPolygon(Vector3 point)
        {
            bool inside = false;
            int count = Vertices.Count;
            if (count < 3) return true;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var vi = Vertices[i];
                var vj = Vertices[j];
                if (((vi.Y > point.Y) != (vj.Y > point.Y)) &&
                    (point.X < (vj.X - vi.X) * (point.Y - vi.Y) / (vj.Y - vi.Y) + vi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private ZoneType type = ZoneType.DynamicCircle;
        private Vector3 center = new Vector3(0, 0, 0);
        private float radius;
        private bool isRelativeToHero;
        private uint maxZDelta = 150;
        private bool bypassObstacles = true;
        private int bypassTimeoutMs = 3500;
        private int stepBackMs = 50;
        private int stepSideMs = 100;
    }
}

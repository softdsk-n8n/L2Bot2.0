using Client.Domain.AI.Combat;
using Client.Domain.Common;
using Client.Domain.Entities;
using Client.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Client.Application.ViewModels
{
    public class AICombatZoneMapViewModel : ObservableObject
    {
        public void MapUpdated(float scale, float viewportWidth, float viewportHeight)
        {
            Scale = scale;
            VieportSize = new Vector3(viewportWidth, viewportHeight, 0);
        }

        public CombatZone Zone { get; }

        public AICombatZoneMapViewModel(CombatZone combatZone, Hero hero)
        {
            Zone = combatZone;
            this.combatZone = combatZone;
            this.hero = hero;

            hero.Transform.Position.PropertyChanged += HeroPosition_PropertyChanged;
            combatZone.PropertyChanged += CombatZone_PropertyChanged;
            combatZone.Vertices.CollectionChanged += Vertices_CollectionChanged;
        }

        private void CombatZone_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged("ScreenVertices");
        }

        private void Vertices_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged("ScreenVertices");
        }

        public List<Point> ScreenVertices
        {
            get
            {
                if (combatZone.Type == ZoneType.Free) return new List<Point>();
                var heroPos = hero.Transform.Position;
                var pts = new List<Point>();
                switch (combatZone.Type)
                {
                    case ZoneType.DynamicCircle:
                        var center = combatZone.IsRelativeToHero ? heroPos : combatZone.Center;
                        int segments = 16;
                        for (int i = 0; i < segments; i++)
                        {
                            double angle = 2 * Math.PI * i / segments;
                            float wx = center.X + combatZone.Radius * (float)Math.Cos(angle);
                            float wy = center.Y + combatZone.Radius * (float)Math.Sin(angle);
                            pts.Add(new Point(
                                (wx - heroPos.X) / scale + (vieportSize.X / 2),
                                (wy - heroPos.Y) / scale + (vieportSize.Y / 2)));
                        }
                        break;
                    case ZoneType.FixedPolygon:
                        foreach (var v in combatZone.Vertices)
                        {
                            pts.Add(new Point(
                                (v.X - heroPos.X) / scale + (vieportSize.X / 2),
                                (v.Y - heroPos.Y) / scale + (vieportSize.Y / 2)));
                        }
                        break;
                }
                return pts;
            }
        }

        public float Scale
        {
            get => scale;
            set
            {
                if (scale != value)
                {
                    scale = value;
                    OnPropertyChanged("ScreenVertices");
                }
            }
        }

        public Vector3 VieportSize
        {
            get => vieportSize;
            set
            {
                if (vieportSize != value)
                {
                    vieportSize = value;
                    OnPropertyChanged("ScreenVertices");
                }
            }
        }

        private void HeroPosition_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged("ScreenVertices");
        }

        private readonly CombatZone combatZone;
        private readonly Hero hero;
        private float scale = 1;
        private Vector3 vieportSize = new Vector3(0, 0, 0);
    }
}

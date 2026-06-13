using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Domain.Common;

namespace Client.Domain.Entities
{
    public class PartyMember : ObservableObject
    {
        private uint objectId;
        private string name = "";
        private int level;
        private int classId;
        private int hp;
        private int hpMax;
        private int mp;
        private int mpMax;
        private int cp;
        private int cpMax;

        public uint ObjectId { get => objectId; set => objectId = value; }
        public string Name { get => name; set => name = value; }
        public int Level { get => level; set => level = value; }
        public int ClassId { get => classId; set => classId = value; }
        public int Hp { get => hp; set => hp = value; }
        public int HpMax { get => hpMax; set => hpMax = value; }
        public int Mp { get => mp; set => mp = value; }
        public int MpMax { get => mpMax; set => mpMax = value; }
        public int Cp { get => cp; set => cp = value; }
        public int CpMax { get => cpMax; set => cpMax = value; }
    }
}

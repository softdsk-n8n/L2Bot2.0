#pragma once

#include "pch.h"

namespace L2
{
	enum class UserType : int32_t
	{
		NPC = 1,
		USER = 0
	};

	enum class Race : int32_t
	{
		DARK_ELF = 2,
		DWARF = 4,
		ELF = 1,
		HUMAN = 0,
		ORC = 3
	};

	enum class Gender : int32_t
	{
		FEMALE = 1,
		MALE = 0
	};

	enum class ItemSlot : int32_t
	{
		BABYPET = 4194304,
		BACK = 8192,
		CHEST = 1024,
		DHAIR = 524288,
		FACE = 262144,
		FEET = 4096,
		FULL_ARMOR = 32768,
		GLOVES = 512,
		HAIR = 65536,
		HATCHLING = 1048576,
		HEAD = 64,
		L_EAR = 4,
		L_FINGER = 32,
		L_HAND = 256,
		LEGS = 2048,
		LR_HAND = 16384,
		NECK = 8,
		NONE = 0,
		R_EAR = 2,
		R_FINGER = 16,
		LoR_EAR = L_EAR | R_EAR,
		LoR_FINGER = L_FINGER | R_FINGER,
		R_HAND = 128,
		STRIDER = 2097152,
		UNDERWEAR = 1,
		WOLF = 131072
	};

	enum class ItemDataType : int32_t
	{
		ARMOR = 1,
		ETC = 2,
		WEAPON = 0
	};

	enum class ItemType2 : int16_t
	{
		ACCESSORY = 2,
		MONEY = 4,
		OTHER = 5,
		PET_BABY = 9,
		PET_HATCHLING = 7,
		PET_STRIDER = 8,
		PET_WOLF = 6,
		QUEST = 3,
		SHIELD_ARMOR = 1,
		WEAPON = 0
	};

	enum class CrystalType : int32_t
	{
		A = 4,
		B = 3,
		C = 2,
		D = 1,
		NG = 0,
		S = 5,
		NONE = -1
	};

	enum class WeaponType : int32_t
	{
		BLUNT = 2,
		BOW = 6,
		DAGGER = 3,
		DUALSWORD = 8,
		ETC = 7,
		FISHING_ROD = 10,
		FIST = 5,
		PET = 9,
		POLE = 4,
		SHIELD = 0,
		SWORD = 1
	};

	enum class ArmorType : int32_t
	{
		NONE = 0,
		HEAVY = 2,
		LIGHT = 1,
		ROBE = 3
	};

	class UserWear
	{
	public:
		char pad_0000[4]; //0x0000
		int32_t leftEarring; //0x0004
		int32_t rightEarring; //0x0008
		int32_t neclace; //0x000C
		int32_t leftRing; //0x0010
		int32_t rightRing; //0x0014
		int32_t helmet; //0x0018
		int32_t weapon; //0x001C
		int32_t shield; //0x0020
		int32_t gloves; //0x0024
		int32_t breastplate; //0x0028
		int32_t gaiters; //0x002C
		int32_t boots; //0x0030
		char pad_0034[64]; //0x0034
	}; //Size: 0x0074

	class FColor
	{
	public:
		uint8_t r; //0x0000
		uint8_t g; //0x0001
		uint8_t b; //0x0002
		uint8_t a; //0x0003
	}; //Size: 0x0004

	class FVector
	{
	public:
		float x = 0; //0x0000
		float y = 0; //0x0004
		float z = 0; //0x0008
	}; //Size: 0x000C

	class FRotator
	{
	public:
		int32_t Pitch; //0x0000
		int32_t Yaw; //0x0004
		int32_t Roll; //0x0008
	}; //Size: 0x000C

#pragma pack(push, 1)
	struct NetworkPacket
	{
		unsigned char id, _padding1, exid, _padding2;
		unsigned short size, _padding3;
		unsigned char* data;
	};
#pragma pack(pop)

	struct SystemMessagePacket : NetworkPacket
	{
		enum class Type
		{
			SWEEPER_FAILED = 343,
			ALREADY_SPOILED = 357,
			SWEEP_SUCCESS = 608,
			SWEEP_SUCCESS2 = 609,
			SPOIL_SUCCESS = 612,
			CANNOT_SPOIL = 661,
			NO_SWEEPER_PRIORITY = 683,
		};

		const uint32_t GetMessageId() const
		{
			return ((uint32_t*)data)[0];
		}
	};

	enum class NetworkPacketId
	{
		SYSTEM_MESSAGE = 0x64,
		PARTY_SMALL_WINDOW_ALL = 0x47,
		PARTY_SMALL_WINDOW_UPDATE = 0x4E,
		PARTY_SMALL_WINDOW_ADD = 0x39,
		PARTY_SMALL_WINDOW_DELETE = 0x4B,
	};

	/// Parser for PartySmallWindowUpdate / PartySmallWindowAdd packets.
	/// Format: D objectId, S name, D cp, D cpMax, D hp, D hpMax, D mp, D mpMax, D level, D classId
	struct PartySmallWindowPacket : NetworkPacket
	{
		uint32_t GetObjectId() const { return ReadDword(0); }
		std::wstring GetName() const { return ReadString(4); }
		uint32_t GetCp() const { return ReadDword(4 + nameLenBytes); }
		uint32_t GetCpMax() const { return ReadDword(8 + nameLenBytes); }
		uint32_t GetHp() const { return ReadDword(12 + nameLenBytes); }
		uint32_t GetHpMax() const { return ReadDword(16 + nameLenBytes); }
		uint32_t GetMp() const { return ReadDword(20 + nameLenBytes); }
		uint32_t GetMpMax() const { return ReadDword(24 + nameLenBytes); }
		uint32_t GetLevel() const { return ReadDword(28 + nameLenBytes); }
		uint32_t GetClassId() const { return ReadDword(32 + nameLenBytes); }

	private:
		// L2 strings: 2-byte length prefix followed by UTF-16 chars, null-terminated.
		mutable int nameLenBytes = -1;

		int GetNameLengthBytes() const
		{
			if (nameLenBytes >= 0) return nameLenBytes;
			// Read 2-byte length prefix at offset 4
			uint16_t charLen = data[4] | (data[5] << 8);
			nameLenBytes = 2 + (int)charLen * 2 + 2; // length prefix + chars + null terminator
			return nameLenBytes;
		}

		uint32_t ReadDword(int offset) const
		{
			return ((uint32_t*)&data[offset])[0];
		}

		std::wstring ReadString(int offset) const
		{
			uint16_t charLen = data[offset] | (data[offset + 1] << 8);
			const wchar_t* chars = (const wchar_t*)&data[offset + 2];
			return std::wstring(chars, charLen);
		}
	};

	/// Parser for PartySmallWindowDelete packet. Format: D objectId, S name
	struct PartySmallWindowDeletePacket : NetworkPacket
	{
		uint32_t GetObjectId() const { return ((uint32_t*)data)[0]; }
		std::wstring GetName() const
		{
			uint16_t charLen = data[4] | (data[5] << 8);
			const wchar_t* chars = (const wchar_t*)&data[6];
			return std::wstring(chars, charLen);
		}
	};

	/// Parser for PartySmallWindowAll packet.
	/// Format: D leaderId, D lootDist, D memberCount
	/// After header: memberCount entries in same format as PartySmallWindowUpdate
	struct PartySmallWindowAllPacket : NetworkPacket
	{
		uint32_t GetLeaderId() const { return ((uint32_t*)data)[0]; }
		uint32_t GetLootDistribution() const { return ((uint32_t*)data)[1]; }
		uint32_t GetMemberCount() const { return ((uint32_t*)data)[2]; }
	};
}
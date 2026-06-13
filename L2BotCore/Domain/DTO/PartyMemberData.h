#pragma once

#include <cstdint>
#include <string>

namespace L2Bot::Domain::DTO
{
	struct PartyMemberData
	{
		const uint32_t objectId = 0;
		const std::wstring name = L"";
		const int32_t level = 0;
		const int32_t classId = 0;
		const int32_t hp = 0;
		const int32_t hpMax = 0;
		const int32_t mp = 0;
		const int32_t mpMax = 0;
		const int32_t cp = 0;
		const int32_t cpMax = 0;
	};
}

#include "pch.h"
#include "../../../Common/apihook.h"
#include "NetworkHandlerWrapper.h"
#include "ProcessManipulation.h"
#include "Domain/Services/ServiceLocator.h"
#include "Domain/Exceptions.h"
#include "Domain/Events/ChatMessageCreatedEvent.h"
#include "Domain/Events/PartyMemberUpdatedEvent.h"
#include "Domain/DTO/ChatMessageData.h"
#include "Domain/DTO/PartyMemberData.h"
#include "Domain/Enums/ChatChannelEnum.h"

using namespace L2Bot::Domain;

namespace Interlude
{
	void* NetworkHandlerWrapper::originalInitAddress = 0;
	NetworkHandlerWrapper::NetworkHandler* NetworkHandlerWrapper::_target = 0;

	void(__thiscall* NetworkHandlerWrapper::__Init)(NetworkHandler*, float) = 0;
	Item* (__thiscall* NetworkHandlerWrapper::__GetNextItem)(NetworkHandler*, float, int) = 0;
	User* (__thiscall* NetworkHandlerWrapper::__GetNextCreature)(NetworkHandler*, float, int) = 0;
	int(__thiscall* NetworkHandlerWrapper::__AddNetworkQueue)(NetworkHandler*, L2::NetworkPacket*) = 0;
	int(__thiscall* NetworkHandlerWrapper::__RequestItemList)(NetworkHandler*) = 0;
	User* (__thiscall* NetworkHandlerWrapper::__GetUser)(NetworkHandler*, int) = 0;
	Item* (__thiscall* NetworkHandlerWrapper::__GetItem)(NetworkHandler*, int) = 0;
	void(__thiscall* NetworkHandlerWrapper::__Action)(NetworkHandler*, int, L2::FVector, int) = 0;
	void(__thiscall* NetworkHandlerWrapper::__MTL)(NetworkHandler*, APawn*, L2::FVector, L2::FVector, void*, int) = 0;
	void(__thiscall* NetworkHandlerWrapper::__RequestMagicSkillUse)(NetworkHandler*, L2ParamStack&) = 0;
	int(__thiscall* NetworkHandlerWrapper::__RequestUseItem)(NetworkHandler*, L2ParamStack&) = 0;
	void(__thiscall* NetworkHandlerWrapper::__RequestAutoSoulShot)(NetworkHandler*, L2ParamStack&) = 0;
	void(__thiscall* NetworkHandlerWrapper::__ChangeWaitType)(NetworkHandler*, int) = 0;
	void(__thiscall* NetworkHandlerWrapper::__RequestRestartPoint)(NetworkHandler*, L2ParamStack&) = 0;

	Item* NetworkHandlerWrapper::GetNextItem(float_t radius, int prevId) const
	{
		__try {
			if (__GetNextItem && _target) {
				return (*__GetNextItem)(_target, radius, prevId);
			}
			return 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw CriticalRuntimeException(L"UNetworkHandler::GetNextItem failed");
		}
	}

	User* NetworkHandlerWrapper::GetNextCreature(float_t radius, int prevId) const
	{
		__try {
			if (__GetNextCreature && _target) {
				return (*__GetNextCreature)(_target, radius, prevId);
			}
			return 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::GetNextCreature failed");
		}
	}

	User* NetworkHandlerWrapper::GetHero() const
	{
		const auto creatures = FindAllObjects<User*>(0.1f, [this](float_t radius, int32_t prevId) {
			return GetNextCreature(radius, prevId);
		});

		for (const auto& kvp : creatures)
		{
			const auto& creature = static_cast<User*>(kvp.second);
			if (creature->userType == L2::UserType::USER && creature->lvl > 0)
			{
				return creature;
			}
		}
		return 0;
	}

	User* NetworkHandlerWrapper::GetUser(int objectId) const
	{
		__try {
			if (__GetUser && _target) {
				return (*__GetUser)(_target, objectId);
			}
			return 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::GetUser failed");
		}
	}

	Item* NetworkHandlerWrapper::GetItem(int objectId) const
	{
		__try {
			if (__GetItem && _target) {
				return (*__GetItem)(_target, objectId);
			}
			return 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::GetItem failed");
		}
	}

	void NetworkHandlerWrapper::MTL(APawn* self, L2::FVector dst, L2::FVector src, void* terrainInfo, int unk1) const
	{
		__try {
			if (__MTL && _target) {
				(*__MTL)(_target, self, dst, src, terrainInfo, unk1);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::MTL failed");
		}
	}

	void NetworkHandlerWrapper::Action(int objectId, L2::FVector objectLocation, int unk) const
	{
		__try {
			if (__Action && _target) {
				(*__Action)(_target, objectId, objectLocation, unk);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::Action failed");
		}
	}

	void NetworkHandlerWrapper::RequestMagicSkillUse(L2ParamStack& stack) const
	{
		__try {
			if (__RequestMagicSkillUse && _target) {
				(*__RequestMagicSkillUse)(_target, stack);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::RequestMagicSkillUse failed");
		}
	}

	int NetworkHandlerWrapper::RequestUseItem(L2ParamStack& stack) const
	{
		__try {
			if (__RequestUseItem && _target) {
				return (*__RequestUseItem)(_target, stack);
			}
			return 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::RequestUseItem failed");
		}
	}

	void NetworkHandlerWrapper::RequestAutoSoulShot(L2ParamStack& stack) const
	{
		__try {
			if (__RequestAutoSoulShot && _target) {
				(*__RequestAutoSoulShot)(_target, stack);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::RequestAutoSoulShot failed");
		}
	}

	void NetworkHandlerWrapper::ChangeWaitType(int type) const
	{
		__try {
			if (__ChangeWaitType && _target) {
				(*__ChangeWaitType)(_target, type);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::ChangeWaitType failed");
		}
	}

	int NetworkHandlerWrapper::RequestItemList() const
	{
		__try {
			if (__RequestItemList && _target) {
				return (*__RequestItemList)(_target);
			}
			return 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::RequestItemList failed");
		}
	}

	void NetworkHandlerWrapper::RequestRestartPoint(L2ParamStack& stack) const
	{
		__try {
			if (__RequestRestartPoint && _target) {
				(*__RequestRestartPoint)(_target, stack);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			throw RuntimeException(L"UNetworkHandler::RequestRestartPoint failed");
		}
	}

	void NetworkHandlerWrapper::Init(HMODULE hModule)
	{
		void* initAddress = GetProcAddress(hModule, "?Tick@UNetworkHandler@@UAEXM@Z");
		originalInitAddress = splice(initAddress, __Init_hook);
		(FARPROC&)__Init = (FARPROC)initAddress;

		(FARPROC&)__GetNextItem = GetProcAddress(hModule, "?GetNextItem@UNetworkHandler@@UAEPAUItem@@MH@Z");
		(FARPROC&)__GetNextCreature = GetProcAddress(hModule, "?GetNextCreature@UNetworkHandler@@UAEPAUUser@@MH@Z");
		(FARPROC&)__RequestItemList = GetProcAddress(hModule, "?RequestItemList@UNetworkHandler@@UAEHXZ");
		(FARPROC&)__GetUser = GetProcAddress(hModule, "?GetUser@UNetworkHandler@@UAEPAUUser@@H@Z");
		(FARPROC&)__GetItem = GetProcAddress(hModule, "?GetItem@UNetworkHandler@@UAEPAUItem@@H@Z");
		(FARPROC&)__MTL = GetProcAddress(hModule, "?MTL@UNetworkHandler@@UAEXPAVAActor@@VFVector@@10H@Z");
		(FARPROC&)__Action = GetProcAddress(hModule, "?Action@UNetworkHandler@@UAEXHVFVector@@H@Z");
		(FARPROC&)__RequestMagicSkillUse = GetProcAddress(hModule, "?RequestMagicSkillUse@UNetworkHandler@@UAEXAAVL2ParamStack@@@Z");
		(FARPROC&)__RequestUseItem = GetProcAddress(hModule, "?RequestUseItem@UNetworkHandler@@UAEHAAVL2ParamStack@@@Z");
		(FARPROC&)__RequestAutoSoulShot = GetProcAddress(hModule, "?RequestAutoSoulShot@UNetworkHandler@@UAEXAAVL2ParamStack@@@Z");
		(FARPROC&)__ChangeWaitType = GetProcAddress(hModule, "?ChangeWaitType@UNetworkHandler@@UAEXH@Z");
		(FARPROC&)__RequestRestartPoint = GetProcAddress(hModule, "?RequestRestartPoint@UNetworkHandler@@UAEXAAVL2ParamStack@@@Z");
		
		(FARPROC&)__AddNetworkQueue = (FARPROC)splice(
			GetProcAddress(hModule, "?AddNetworkQueue@UNetworkHandler@@UAEHPAUNetworkPacket@@@Z"), __AddNetworkQueue_hook
		);
		Services::ServiceLocator::GetInstance().GetLogger()->Info(L"UNetworkHandler hooks initialized");
	}

	void NetworkHandlerWrapper::Restore()
	{
		restore(originalInitAddress);
		restore((void*&)__AddNetworkQueue);
		Services::ServiceLocator::GetInstance().GetLogger()->Info(L"UNetworkHandler hooks restored");
	}

	void __fastcall NetworkHandlerWrapper::__Init_hook(NetworkHandler* This, int /*edx*/, float unk)
	{
		if (_target == 0) {
			_target = This;
			Services::ServiceLocator::GetInstance().GetLogger()->Info(L"UNetworkHandler pointer {:#010x} obtained", (int)_target);

			InjectLibrary::StopCurrentProcess();
			restore(originalInitAddress);
			InjectLibrary::StartCurrentProcess();

			(*__Init)(This, unk);
		}
	}

	int __fastcall NetworkHandlerWrapper::__AddNetworkQueue_hook(NetworkHandler* This, int, L2::NetworkPacket* packet)
	{
		if (packet)
		{
			// Try both common SystemMessage opcodes: 0x62 (standard Interlude) and 0x64
			if (packet->id == 0x62 || packet->id == 0x64)
			{
				const auto sysMsg = reinterpret_cast<L2::SystemMessagePacket*>(packet);
				const auto msgId = sysMsg->GetMessageId();

				// Filter: only forward spoil/sweep related messages to avoid flooding C# with system spam
				bool isSpoilSweep = (msgId == 343 || msgId == 357 || msgId == 608 || msgId == 609 || msgId == 612 || msgId == 661 || msgId == 683);

				if (isSpoilSweep)
				{
					std::wstring text = L"System message #" + std::to_wstring(msgId);

					Services::ServiceLocator::GetInstance().GetEventDispatcher()->Dispatch(
						Events::ChatMessageCreatedEvent{
							DTO::ChatMessageData{
								msgId,
								static_cast<uint8_t>(Enums::ChatChannelEnum::announcement),
								L"System",
								text
							}
						}
					);

					Services::ServiceLocator::GetInstance().GetLogger()->Info(L"SystemMessage forwarded: id={} opcode=0x{:02x}", msgId, packet->id);
				}
				else
				{
					// Log first few non-spoil system messages for opcode discovery
					static int sysMsgLogCount = 0;
					if (sysMsgLogCount++ < 10)
					{
						Services::ServiceLocator::GetInstance().GetLogger()->Info(L"SystemMessage (not forwarded): id={} opcode=0x{:02x}", msgId, packet->id);
					}
				}
			}

			// Party packet handling
			if (packet->id == static_cast<unsigned char>(L2::NetworkPacketId::PARTY_SMALL_WINDOW_ALL))
			{
				const auto partyAll = reinterpret_cast<L2::PartySmallWindowAllPacket*>(packet);
				auto memberCount = partyAll->GetMemberCount();
				Services::ServiceLocator::GetInstance().GetLogger()->Info(L"PartySmallWindowAll: {} members (leader={}, lootDist={})", memberCount, partyAll->GetLeaderId(), partyAll->GetLootDistribution());

				// Parse each member entry: same format as PartySmallWindowUpdate, starting after 12-byte header
				int offset = 12;
				for (uint32_t i = 0; i < memberCount && offset < packet->size; i++)
				{
					auto member = reinterpret_cast<L2::PartySmallWindowPacket*>(packet);
					// Override data pointer to point at current member entry
					// ... this won't work directly. Let's use raw parsing instead.
					break; // TODO: proper iterative parsing
				}
			}
			else if (packet->id == static_cast<unsigned char>(L2::NetworkPacketId::PARTY_SMALL_WINDOW_UPDATE))
			{
				const auto pkt = reinterpret_cast<L2::PartySmallWindowPacket*>(packet);
				Services::ServiceLocator::GetInstance().GetEventDispatcher()->Dispatch(
					Events::PartyMemberUpdatedEvent{
						DTO::PartyMemberData{
							pkt->GetObjectId(),
							pkt->GetName(),
							(int32_t)pkt->GetLevel(),
							(int32_t)pkt->GetClassId(),
							(int32_t)pkt->GetHp(),
							(int32_t)pkt->GetHpMax(),
							(int32_t)pkt->GetMp(),
							(int32_t)pkt->GetMpMax(),
							(int32_t)pkt->GetCp(),
							(int32_t)pkt->GetCpMax()
						},
						Events::PartyMemberAction::Updated
					}
				);
				Services::ServiceLocator::GetInstance().GetLogger()->Info(L"PartySmallWindowUpdate: {} HP={}/{} MP={}/{}", pkt->GetName(), pkt->GetHp(), pkt->GetHpMax(), pkt->GetMp(), pkt->GetMpMax());
			}
			else if (packet->id == static_cast<unsigned char>(L2::NetworkPacketId::PARTY_SMALL_WINDOW_ADD))
			{
				const auto pkt = reinterpret_cast<L2::PartySmallWindowPacket*>(packet);
				Services::ServiceLocator::GetInstance().GetEventDispatcher()->Dispatch(
					Events::PartyMemberUpdatedEvent{
						DTO::PartyMemberData{
							pkt->GetObjectId(),
							pkt->GetName(),
							(int32_t)pkt->GetLevel(),
							(int32_t)pkt->GetClassId(),
							(int32_t)pkt->GetHp(),
							(int32_t)pkt->GetHpMax(),
							(int32_t)pkt->GetMp(),
							(int32_t)pkt->GetMpMax(),
							(int32_t)pkt->GetCp(),
							(int32_t)pkt->GetCpMax()
						},
						Events::PartyMemberAction::Added
					}
				);
				Services::ServiceLocator::GetInstance().GetLogger()->Info(L"PartySmallWindowAdd: {} id={}", pkt->GetName(), pkt->GetObjectId());
			}
			else if (packet->id == static_cast<unsigned char>(L2::NetworkPacketId::PARTY_SMALL_WINDOW_DELETE))
			{
				const auto pkt = reinterpret_cast<L2::PartySmallWindowDeletePacket*>(packet);
				Services::ServiceLocator::GetInstance().GetEventDispatcher()->Dispatch(
					Events::PartyMemberUpdatedEvent{
						DTO::PartyMemberData{ pkt->GetObjectId(), pkt->GetName() },
						Events::PartyMemberAction::Removed
					}
				);
				Services::ServiceLocator::GetInstance().GetLogger()->Info(L"PartySmallWindowDelete: {} id={}", pkt->GetName(), pkt->GetObjectId());
			}
		}

		return (*__AddNetworkQueue)(This, packet);
	}
}

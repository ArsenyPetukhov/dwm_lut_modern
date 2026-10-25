#pragma once

#include <stdint.h>

// Pure, allocation-free LUT routing helpers. Coordinate selectors require TLut.left,
// TLut.top, and TLut.isHdr; the identity selector's requirements are listed below.
// Keeping policy here makes the safety-critical selection rules deterministic and testable without
// loading D3D, DWM, or the injected DLL.

struct LutDisplayIdentity
{
	uint64_t adapterLuid;
	uint32_t sourceId;
	uint32_t targetId;
};

// CCD target IDs identify outputs within an adapter. Source IDs are diagnostic:
// desktop reconfiguration may change a source without changing its output.
// TLut must expose displayIdentity, hasDisplayIdentity, and isHdr; coordinates
// deliberately are not part of this selection path.
template <typename TLut>
int FindDisplayIdentityDomainLut(const TLut* luts, int lutCount,
	const LutDisplayIdentity& identity, bool hdr)
{
	if (luts == nullptr || lutCount <= 0 || identity.targetId == UINT32_MAX)
	{
		return -1;
	}

	int match = -1;
	for (int i = 0; i < lutCount; ++i)
	{
		if (!luts[i].hasDisplayIdentity || luts[i].isHdr != hdr ||
			luts[i].displayIdentity.targetId == UINT32_MAX ||
			luts[i].displayIdentity.adapterLuid != identity.adapterLuid ||
			luts[i].displayIdentity.targetId != identity.targetId)
		{
			continue;
		}
		if (match != -1)
		{
			return -1;
		}
		match = i;
	}
	return match;
}

template <typename TLut>
int FindSingleMonitorDomainLut(const TLut* luts, int lutCount, int connectedMonitorCount, bool hdr)
{
	if (connectedMonitorCount != 1 || luts == nullptr || lutCount <= 0)
	{
		return -1;
	}

	int match = -1;
	for (int i = 0; i < lutCount; i++)
	{
		if (luts[i].isHdr != hdr)
		{
			continue;
		}

		// Duplicate candidates are ambiguous even on one display. Fail closed.
		if (match != -1)
		{
			return -1;
		}
		match = i;
	}
	return match;
}
template <typename TLut>
int FindExactDisplayDomainLut(const TLut* luts, int lutCount, int left, int top, bool hdr)
{
	if (luts == nullptr || lutCount <= 0)
	{
		return -1;
	}

	int match = -1;
	for (int i = 0; i < lutCount; i++)
	{
		if (luts[i].left != left || luts[i].top != top || luts[i].isHdr != hdr)
		{
			continue;
		}

		// Never choose arbitrarily between duplicate display/domain candidates.
		if (match != -1)
		{
			return -1;
		}
		match = i;
	}
	return match;
}

#pragma once

// Pure, allocation-free LUT routing helpers. TLut must expose left, top, and isHdr fields.
// Keeping policy here makes the safety-critical selection rules deterministic and testable without
// loading D3D, DWM, or the injected DLL.

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

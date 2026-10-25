#pragma once

#include "LutRouting.h"
#include <limits.h>

// The position associates a staged filename with its manifest row. Runtime output
// routing uses display-adapter LUID + target ID, never these coordinates.
struct MonitorManifestRow
{
	int left;
	int top;
	LutDisplayIdentity displayIdentity;
};

namespace MonitorManifestDetail
{
	inline const char* Find(const char* begin, const char* end, char value)
	{
		for (const char* cursor = begin; cursor != end; ++cursor)
		{
			if (*cursor == value) return cursor;
		}
		return end;
	}

	inline bool Unsigned(const char* begin, const char* end, unsigned int base,
		uint64_t maximum, uint64_t* value)
	{
		if (begin == end) return false;
		uint64_t parsed = 0;
		for (const char* cursor = begin; cursor != end; ++cursor)
		{
			unsigned int digit;
			if (*cursor >= '0' && *cursor <= '9') digit = static_cast<unsigned int>(*cursor - '0');
			else if (*cursor >= 'a' && *cursor <= 'f') digit = static_cast<unsigned int>(*cursor - 'a') + 10;
			else if (*cursor >= 'A' && *cursor <= 'F') digit = static_cast<unsigned int>(*cursor - 'A') + 10;
			else return false;
			if (digit >= base || digit > maximum || parsed > (maximum - digit) / base) return false;
			parsed = parsed * base + digit;
		}
		*value = parsed;
		return true;
	}

	inline bool SignedInt(const char* begin, const char* end, int* value)
	{
		if (begin == end) return false;
		const bool negative = *begin == '-';
		if (negative) ++begin;
		const uint64_t limit = static_cast<uint64_t>(INT_MAX) + (negative ? 1u : 0u);
		uint64_t magnitude;
		if (!Unsigned(begin, end, 10, limit, &magnitude)) return false;
		if (negative && magnitude == static_cast<uint64_t>(INT_MAX) + 1u) *value = INT_MIN;
		else *value = negative ? -static_cast<int>(magnitude) : static_cast<int>(magnitude);
		return true;
	}
}

// The first four fields are position, GUI source number, display adapter LUID,
// and raw target ID. Requiring all four delimiters rejects the older manifest
// shape. The remaining columns may contain empty or long escaped strings.
// On failure the caller's output is unchanged.
inline bool ParseMonitorManifestRow(const char* line, MonitorManifestRow* row)
{
	if (line == nullptr || row == nullptr) return false;
	const char* tabs[4];
	const char* cursor = line;
	for (unsigned int i = 0; i < 4; ++i)
	{
		while (*cursor != '\0' && *cursor != '\t') ++cursor;
		if (*cursor != '\t') return false;
		tabs[i] = cursor++;
	}

	MonitorManifestRow parsed = {};
	const char* comma = MonitorManifestDetail::Find(line, tabs[0], ',');
	if (comma == tabs[0] ||
		!MonitorManifestDetail::SignedInt(line, comma, &parsed.left) ||
		!MonitorManifestDetail::SignedInt(comma + 1, tabs[0], &parsed.top)) return false;

	uint64_t source;
	uint64_t target;
	uint64_t adapter;
	if (tabs[2] - (tabs[1] + 1) != 16 ||
		!MonitorManifestDetail::Unsigned(tabs[0] + 1, tabs[1], 10, UINT32_MAX, &source) ||
		!MonitorManifestDetail::Unsigned(tabs[1] + 1, tabs[2], 16, UINT64_MAX, &adapter) ||
		!MonitorManifestDetail::Unsigned(tabs[2] + 1, tabs[3], 10, UINT32_MAX - 1u, &target)) return false;

	parsed.displayIdentity.adapterLuid = adapter;
	// The GUI displays source ID + 1. Source IDs are diagnostic only and are
	// deliberately excluded from FindDisplayIdentityDomainLut's output key.
	parsed.displayIdentity.sourceId = source == 0 ? UINT32_MAX : static_cast<uint32_t>(source - 1u);
	parsed.displayIdentity.targetId = static_cast<uint32_t>(target);
	*row = parsed;
	return true;
}

inline bool FindManifestDisplayIdentity(const MonitorManifestRow* rows, int count,
	int left, int top, LutDisplayIdentity* identity)
{
	if (rows == nullptr || count <= 0 || identity == nullptr) return false;
	const MonitorManifestRow* match = nullptr;
	for (int i = 0; i < count; ++i)
	{
		if (rows[i].left != left || rows[i].top != top) continue;
		if (match != nullptr) return false;
		match = &rows[i];
	}
	if (match == nullptr || match->displayIdentity.targetId == UINT32_MAX) return false;
	*identity = match->displayIdentity;
	return true;
}

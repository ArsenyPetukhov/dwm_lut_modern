#pragma once

#include <stdint.h>

// Private DWM interfaces must be identified before any virtual method is called.
// Each pair describes one verified swap-chain implementation and its buffer type.
struct BackBufferVtablePair
{
	uint32_t swapChainVtableRva;
	uint32_t bufferVtableRva;
};

struct BackBufferLayout
{
	BackBufferVtablePair pairs[2];
	uint32_t backBufferSlot;
	uint32_t resourceSlot;
};

using BackBufferReadPointer = bool (*)(void* address, void** value);
using BackBufferInvokePointerMethod = bool (*)(void* object, void* function, void** value);

inline bool BackBufferCheckedAddress(uintptr_t base, uintptr_t offset, uintptr_t* result)
{
	if (base == 0 || offset > UINTPTR_MAX - base || result == nullptr)
	{
		return false;
	}
	*result = base + offset;
	return true;
}

// Callbacks own platform-specific fault containment. Returned interfaces are borrowed;
// the caller must QueryInterface the resource before retaining or releasing anything.
inline void* ResolveBackBufferResource(void* swapChain, uintptr_t moduleBase,
	const BackBufferLayout& layout, BackBufferReadPointer readPointer,
	BackBufferInvokePointerMethod invokePointerMethod)
{
	if (swapChain == nullptr || readPointer == nullptr || invokePointerMethod == nullptr ||
		layout.backBufferSlot == 0 || layout.resourceSlot == 0 ||
		layout.backBufferSlot >= 64 || layout.resourceSlot >= 64)
	{
		return nullptr;
	}

	uintptr_t chainVtables[2] = {};
	uintptr_t bufferVtables[2] = {};
	for (unsigned int i = 0; i < 2; ++i)
	{
		if (layout.pairs[i].swapChainVtableRva == 0 || layout.pairs[i].bufferVtableRva == 0 ||
			!BackBufferCheckedAddress(moduleBase, layout.pairs[i].swapChainVtableRva, &chainVtables[i]) ||
			!BackBufferCheckedAddress(moduleBase, layout.pairs[i].bufferVtableRva, &bufferVtables[i]))
		{
			return nullptr;
		}
	}
	if (chainVtables[0] == chainVtables[1] || bufferVtables[0] == bufferVtables[1])
	{
		return nullptr;
	}

	void* chainVtable = nullptr;
	if (!readPointer(swapChain, &chainVtable))
	{
		return nullptr;
	}
	int pair = -1;
	for (int i = 0; i < 2; ++i)
	{
		if (reinterpret_cast<uintptr_t>(chainVtable) == chainVtables[i])
		{
			pair = i;
			break;
		}
	}
	if (pair == -1)
	{
		return nullptr;
	}

	uintptr_t methodAddress = 0;
	void* method = nullptr;
	void* buffer = nullptr;
	if (!BackBufferCheckedAddress(chainVtables[pair], layout.backBufferSlot * sizeof(void*), &methodAddress) ||
		!readPointer(reinterpret_cast<void*>(methodAddress), &method) || method == nullptr ||
		!invokePointerMethod(swapChain, method, &buffer) || buffer == nullptr)
	{
		return nullptr;
	}

	void* bufferVtable = nullptr;
	if (!readPointer(buffer, &bufferVtable) ||
		reinterpret_cast<uintptr_t>(bufferVtable) != bufferVtables[pair])
	{
		return nullptr;
	}

	method = nullptr;
	void* resource = nullptr;
	if (!BackBufferCheckedAddress(bufferVtables[pair], layout.resourceSlot * sizeof(void*), &methodAddress) ||
		!readPointer(reinterpret_cast<void*>(methodAddress), &method) || method == nullptr ||
		!invokePointerMethod(buffer, method, &resource))
	{
		return nullptr;
	}
	return resource;
}

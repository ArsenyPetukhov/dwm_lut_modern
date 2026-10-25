#include "../../src/lutdwm/BackBufferAccess.h"

#include <stdio.h>
#include <map>
#include <utility>
#include <vector>

namespace
{
	constexpr uintptr_t ModuleBase = 0x10000000;
	const BackBufferLayout Layout = {{{0x3219a0, 0x314200}, {0x3229f0, 0x3142e0}}, 24, 17};

	void* Pointer(uintptr_t value) { return reinterpret_cast<void*>(value); }

	const uintptr_t Chain = 0x20000000;
	const uintptr_t Buffer = 0x20001000;
	const uintptr_t Resource = 0x20002000;
	const uintptr_t GetBackBuffer = 0x10005000;
	const uintptr_t GetResource = 0x10006000;

	struct FakeMemory
	{
		std::map<uintptr_t, void*> cells;
		std::map<std::pair<uintptr_t, uintptr_t>, void*> returns;
		std::vector<std::pair<uintptr_t, uintptr_t>> calls;
		unsigned int reads = 0;
	};
	FakeMemory memory;
	int failures = 0;
	int checks = 0;

	bool ReadPointer(void* address, void** value)
	{
		++memory.reads;
		const auto cell = memory.cells.find(reinterpret_cast<uintptr_t>(address));
		if (cell == memory.cells.end()) return false;
		*value = cell->second;
		return true;
	}

	bool Invoke(void* object, void* method, void** value)
	{
		const auto key = std::make_pair(reinterpret_cast<uintptr_t>(object), reinterpret_cast<uintptr_t>(method));
		memory.calls.push_back(key);
		const auto result = memory.returns.find(key);
		if (result == memory.returns.end()) return false;
		*value = result->second;
		return true;
	}

	void Expect(bool condition, const char* description)
	{
		++checks;
		if (!condition)
		{
			++failures;
			fprintf(stderr, "FAIL: %s\n", description);
		}
	}

	void Reset(unsigned int pair = 0)
	{
		memory = {};
		const uintptr_t chainVtable = ModuleBase + Layout.pairs[pair].swapChainVtableRva;
		const uintptr_t bufferVtable = ModuleBase + Layout.pairs[pair].bufferVtableRva;
		memory.cells[Chain] = Pointer(chainVtable);
		memory.cells[chainVtable + 24 * sizeof(void*)] = Pointer(GetBackBuffer);
		memory.cells[Buffer] = Pointer(bufferVtable);
		memory.cells[bufferVtable + 17 * sizeof(void*)] = Pointer(GetResource);
		memory.returns[{Chain, GetBackBuffer}] = Pointer(Buffer);
		memory.returns[{Buffer, GetResource}] = Pointer(Resource);
	}

	void* Resolve(const BackBufferLayout& layout = Layout, uintptr_t moduleBase = ModuleBase)
	{
		return ResolveBackBufferResource(Pointer(Chain), moduleBase, layout, ReadPointer, Invoke);
	}
}

int main()
{
	for (unsigned int pair = 0; pair < 2; ++pair)
	{
		Reset(pair);
		Expect(Resolve() == Pointer(Resource), "both verified implementations resolve the borrowed resource");
		Expect(memory.calls.size() == 2, "success calls exactly two verified virtual methods");
		Expect(memory.calls[0] == std::make_pair(Chain, GetBackBuffer), "backbuffer method uses the swap-chain receiver");
		Expect(memory.calls[1] == std::make_pair(Buffer, GetResource), "resource method uses the returned buffer receiver");
	}

	Reset();
	memory.cells[Chain] = Pointer(ModuleBase + 0x1234);
	Expect(Resolve() == nullptr && memory.calls.empty(), "unknown swap-chain vtable never receives a virtual call");
	Reset();
	memory.cells[Buffer] = Pointer(ModuleBase + Layout.pairs[1].bufferVtableRva);
	Expect(Resolve() == nullptr && memory.calls.size() == 1, "wrong paired buffer stops before the resource call");
	Reset();
	memory.cells.erase(Chain);
	Expect(Resolve() == nullptr && memory.calls.empty(), "unreadable swap-chain pointer is skipped");
	Reset();
	memory.cells.erase(ModuleBase + Layout.pairs[0].swapChainVtableRva + 24 * sizeof(void*));
	Expect(Resolve() == nullptr && memory.calls.empty(), "unreadable backbuffer slot is skipped");
	Reset();
	memory.cells[ModuleBase + Layout.pairs[0].swapChainVtableRva + 24 * sizeof(void*)] = nullptr;
	Expect(Resolve() == nullptr && memory.calls.empty(), "null backbuffer method is never invoked");
	Reset();
	memory.returns.erase({Chain, GetBackBuffer});
	Expect(Resolve() == nullptr && memory.calls.size() == 1, "failed backbuffer call stops lookup");
	Reset();
	memory.returns[{Chain, GetBackBuffer}] = nullptr;
	Expect(Resolve() == nullptr && memory.calls.size() == 1, "null physical buffer is skipped");
	Reset();
	memory.cells.erase(Buffer);
	Expect(Resolve() == nullptr && memory.calls.size() == 1, "unreadable buffer pointer is skipped");
	Reset();
	memory.cells.erase(ModuleBase + Layout.pairs[0].bufferVtableRva + 17 * sizeof(void*));
	Expect(Resolve() == nullptr && memory.calls.size() == 1, "unreadable resource slot is skipped");
	Reset();
	memory.cells[ModuleBase + Layout.pairs[0].bufferVtableRva + 17 * sizeof(void*)] = nullptr;
	Expect(Resolve() == nullptr && memory.calls.size() == 1, "null resource method is never invoked");
	Reset();
	memory.returns.erase({Buffer, GetResource});
	Expect(Resolve() == nullptr && memory.calls.size() == 2, "failed resource call yields no resource");
	Reset();
	memory.returns[{Buffer, GetResource}] = nullptr;
	Expect(Resolve() == nullptr && memory.calls.size() == 2, "null resource return is skipped");

	Reset();
	BackBufferLayout invalid = {};
	Expect(Resolve(invalid) == nullptr && memory.reads == 0, "empty profile fails before reading memory");
	invalid = Layout;
	invalid.pairs[1].bufferVtableRva = 0;
	Expect(Resolve(invalid) == nullptr && memory.reads == 0, "incomplete profile fails before reading memory");
	invalid = Layout;
	invalid.pairs[1].swapChainVtableRva = invalid.pairs[0].swapChainVtableRva;
	Expect(Resolve(invalid) == nullptr && memory.reads == 0, "duplicate swap-chain types are ambiguous");
	invalid = Layout;
	invalid.pairs[1].bufferVtableRva = invalid.pairs[0].bufferVtableRva;
	Expect(Resolve(invalid) == nullptr && memory.reads == 0, "duplicate buffer types are ambiguous");
	invalid = Layout;
	invalid.backBufferSlot = 0;
	Expect(Resolve(invalid) == nullptr && memory.reads == 0, "slot zero cannot invoke QueryInterface as a pointer getter");
	invalid = Layout;
	invalid.resourceSlot = UINT32_MAX;
	Expect(Resolve(invalid) == nullptr && memory.reads == 0, "unbounded slot is rejected before address arithmetic");
	Expect(Resolve(Layout, 0) == nullptr && memory.reads == 0, "zero module base is rejected");
	Expect(Resolve(Layout, UINTPTR_MAX - 0x100) == nullptr && memory.reads == 0, "module RVA overflow is rejected");
	Expect(ResolveBackBufferResource(nullptr, ModuleBase, Layout, ReadPointer, Invoke) == nullptr && memory.reads == 0,
		"null swap-chain is rejected");
	Expect(ResolveBackBufferResource(Pointer(Chain), ModuleBase, Layout, nullptr, Invoke) == nullptr && memory.reads == 0,
		"missing read callback is rejected");
	Expect(ResolveBackBufferResource(Pointer(Chain), ModuleBase, Layout, ReadPointer, nullptr) == nullptr && memory.reads == 0,
		"missing invoke callback is rejected");

	Reset();
	const uintptr_t overflowingBase = UINTPTR_MAX - Layout.pairs[1].swapChainVtableRva;
	memory.cells[Chain] = Pointer(UINTPTR_MAX);
	Expect(Resolve(Layout, overflowingBase) == nullptr && memory.calls.empty(), "slot address overflow is rejected");
	Expect(memory.reads == 1, "slot overflow occurs after type validation without attempting a slot read");

	printf("BackBufferAccessTests: %d checks, %d failures\n", checks, failures);
	return failures == 0 ? 0 : 1;
}

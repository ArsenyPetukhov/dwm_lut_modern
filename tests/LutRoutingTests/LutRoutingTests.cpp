#include "../../src/lutdwm/LutRouting.h"

#include <stdio.h>

struct TestLut
{
	int left;
	int top;
	bool isHdr;
};

static int failures = 0;
static int checks = 0;

static void ExpectEqual(int expected, int actual)
{
	++checks;
	if (expected != actual)
	{
		failures++;
		fprintf(stderr, "Routing check %d: expected %d, got %d\n", checks, expected, actual);
	}
}

int main()
{
	const TestLut sdrAndHdr[] = { { 0, 0, false }, { 0, 0, true } };
	ExpectEqual(0, FindSingleMonitorDomainLut(sdrAndHdr, 2, 1, false));
	ExpectEqual(1, FindSingleMonitorDomainLut(sdrAndHdr, 2, 1, true));

	const TestLut sdrOnly[] = { { 0, 0, false } };
	ExpectEqual(-1, FindSingleMonitorDomainLut(sdrOnly, 1, 1, true));
	// One configured LUT is not proof of a single-monitor desktop.
	ExpectEqual(-1, FindSingleMonitorDomainLut(sdrOnly, 1, 2, false));

	const TestLut multi[] = { { -1920, 0, false }, { 0, 0, false }, { 0, 0, true } };
	ExpectEqual(0, FindExactDisplayDomainLut(multi, 3, -1920, 0, false));
	ExpectEqual(2, FindExactDisplayDomainLut(multi, 3, 0, 0, true));
	ExpectEqual(-1, FindExactDisplayDomainLut(multi, 3, -1919, 0, false));
	ExpectEqual(-1, FindExactDisplayDomainLut(multi, 3, -1920, 0, true));

	const TestLut duplicate[] = { { 0, 0, false }, { 0, 0, false } };
	ExpectEqual(-1, FindSingleMonitorDomainLut(duplicate, 2, 1, false));
	ExpectEqual(-1, FindExactDisplayDomainLut(duplicate, 2, 0, 0, false));

	// This type has no coordinates: identity routing cannot silently fall back to them.
	struct IdentityLut
	{
		bool isHdr;
		bool hasDisplayIdentity;
		LutDisplayIdentity displayIdentity;
	};
	const uint64_t adapterA = UINT64_C(0x100000001);
	const uint64_t adapterB = UINT64_C(0x200000001);
	const LutDisplayIdentity displayA = {adapterA, 0, 773};
	const IdentityLut identities[] = {
		{false, true, {adapterA, 0, 773}},
		{false, true, {adapterB, 0, 773}},
		{false, true, {adapterA, 0, 774}},
		{true, true, {adapterA, 0, 773}},
		{false, false, {adapterA, 0, 773}},
		{false, true, {adapterA, 0, UINT32_MAX}},
	};
	ExpectEqual(0, FindDisplayIdentityDomainLut(identities, 6, displayA, false));
	ExpectEqual(1, FindDisplayIdentityDomainLut(identities, 6, {adapterB, 0, 773}, false));
	ExpectEqual(2, FindDisplayIdentityDomainLut(identities, 6, {adapterA, 0, 774}, false));
	ExpectEqual(3, FindDisplayIdentityDomainLut(identities, 6, displayA, true));
	ExpectEqual(-1, FindDisplayIdentityDomainLut(identities, 6, {adapterA, 0, 774}, true));
	ExpectEqual(-1, FindDisplayIdentityDomainLut(identities, 6, {adapterA, 0, 775}, false));
	ExpectEqual(-1, FindDisplayIdentityDomainLut(identities, 6, {3, 0, 773}, false));
	// Source numbering differences do not change the output's canonical identity.
	ExpectEqual(0, FindDisplayIdentityDomainLut(identities, 6, {adapterA, 1, 773}, false));
	ExpectEqual(0, FindDisplayIdentityDomainLut(identities, 6, {adapterA, UINT32_MAX, 773}, false));
	ExpectEqual(-1, FindDisplayIdentityDomainLut(identities, 6, {adapterA, 0, UINT32_MAX}, false));
	ExpectEqual(-1, FindDisplayIdentityDomainLut(identities, 0, displayA, false));
	ExpectEqual(-1, FindDisplayIdentityDomainLut(identities, -1, displayA, false));
	ExpectEqual(-1, FindDisplayIdentityDomainLut<IdentityLut>(nullptr, 1, displayA, false));
	const IdentityLut unidentified[] = {{false, false, displayA}};
	ExpectEqual(-1, FindDisplayIdentityDomainLut(unidentified, 1, displayA, false));
	const IdentityLut duplicateIdentity[] = {
		{false, true, displayA}, {false, true, {adapterA, 1, 773}},
	};
	ExpectEqual(-1, FindDisplayIdentityDomainLut(duplicateIdentity, 2, displayA, false));
	// Neither target zero nor LUID zero is rejected without a documented sentinel contract.
	const IdentityLut zeroIdentity[] = {{false, true, {0, UINT32_MAX, 0}}};
	ExpectEqual(0, FindDisplayIdentityDomainLut(zeroIdentity, 1, {0, 0, 0}, false));

	printf("LutRoutingTests: %d checks, %d failures\n", checks, failures);

	return failures == 0 ? 0 : 1;
}

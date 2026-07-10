#include "../../src/lutdwm/LutRouting.h"

struct TestLut
{
	int left;
	int top;
	bool isHdr;
};

static int failures = 0;

static void ExpectEqual(int expected, int actual)
{
	if (expected != actual)
	{
		failures++;
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

	return failures == 0 ? 0 : 1;
}

#include "../../src/lutdwm/MonitorManifest.h"
#include <stdio.h>

static int checks = 0;
static int failures = 0;

static void Expect(bool condition, const char* description)
{
	++checks;
	if (!condition)
	{
		++failures;
		printf("FAIL: %s\n", description);
	}
}

static void Reject(const char* line, const char* description)
{
	MonitorManifestRow row = { 123, 456, { 789, 10, 11 } };
	Expect(!ParseMonitorManifestRow(line, &row), description);
	Expect(row.left == 123 && row.top == 456 && row.displayIdentity.adapterLuid == 789 &&
		row.displayIdentity.sourceId == 10 && row.displayIdentity.targetId == 11,
		"failed parse preserves output");
}

int main()
{
	MonitorManifestRow row = {};
	Expect(ParseMonitorManifestRow("0,0\t1\t0000000000011D85\t773\tBMD HDMI\t\t\r\n", &row),
		"parse reporter's output identity");
	Expect(row.left == 0 && row.top == 0, "reporter position");
	Expect(row.displayIdentity.adapterLuid == 0x11D85, "reporter adapter LUID");
	Expect(row.displayIdentity.sourceId == 0, "normalize GUI source number");
	Expect(row.displayIdentity.targetId == 773, "preserve raw target ID");
	Expect(ParseMonitorManifestRow("-1920,-1200\t2\tfEdCbA9876543210\t4294967294\t", &row),
		"negative coordinates, mixed hex, maximum usable target, empty remaining fields");
	Expect(row.left == -1920 && row.top == -1200 && row.displayIdentity.adapterLuid == UINT64_C(0xFEDCBA9876543210) &&
		row.displayIdentity.sourceId == 1 && row.displayIdentity.targetId == UINT32_MAX - 1u,
		"parsed signed and unsigned fields");
	Expect(ParseMonitorManifestRow("-2147483648,2147483647\t4294967295\tFFFFFFFFFFFFFFFF\t0\t", &row),
		"signed bounds and unsigned source bounds");
	Expect(row.left == INT_MIN && row.top == INT_MAX && row.displayIdentity.adapterLuid == UINT64_MAX &&
		row.displayIdentity.sourceId == UINT32_MAX - 1u && row.displayIdentity.targetId == 0,
		"integer bounds remain exact");
	Expect(ParseMonitorManifestRow("0,-0\t0\t0000000000000000\t0\t", &row),
		"zero adapter and source are not invented invalid-output rules");
	Expect(row.displayIdentity.sourceId == UINT32_MAX, "zero GUI source becomes unknown diagnostic source");
	Expect(!ParseMonitorManifestRow("0,0\t1\t0000000000000001\t1\t", nullptr), "null output rejects");
	Reject(nullptr, "null line rejects");
	Reject("", "empty line rejects");
	Reject("position\tsource_id\tadapter_luid\ttarget_id\tdevice_path", "manifest header rejects");
	Reject("\xEF\xBB\xBFposition\tsource_id\tadapter_luid\ttarget_id\tdevice_path", "BOM header rejects");
	Reject("0,0\t1\t773\tdevice\tname", "old manifest shape rejects");
	Reject("0,0\t1\t0000000000011D85\t773", "truncated row without fourth delimiter rejects");
	Reject("0,0\t1\t0000000000011D85\t\t", "empty target rejects");
	Reject("0,0\t\t0000000000011D85\t773\t", "empty source rejects");
	Reject("0,0\t1\t\t773\t", "empty adapter rejects");
	Reject("0\t1\t0000000000011D85\t773\t", "missing coordinate rejects");
	Reject(",0\t1\t0000000000011D85\t773\t", "empty first coordinate rejects");
	Reject("0,\t1\t0000000000011D85\t773\t", "empty second coordinate rejects");
	Reject("0,0,1\t1\t0000000000011D85\t773\t", "extra coordinate rejects");
	Reject("-,0\t1\t0000000000011D85\t773\t", "bare coordinate sign rejects");
	Reject("+0,0\t1\t0000000000011D85\t773\t", "noncanonical plus sign rejects");
	Reject(" 0,0\t1\t0000000000011D85\t773\t", "leading whitespace rejects");
	Reject("0,0 \t1\t0000000000011D85\t773\t", "trailing coordinate whitespace rejects");
	Reject("2147483648,0\t1\t0000000000011D85\t773\t", "positive coordinate overflow rejects");
	Reject("-2147483649,0\t1\t0000000000011D85\t773\t", "negative coordinate overflow rejects");
	Reject("0,0\t-1\t0000000000011D85\t773\t", "negative unsigned source rejects");
	Reject("0,0\t4294967296\t0000000000011D85\t773\t", "source overflow rejects");
	Reject("0,0\t1x\t0000000000011D85\t773\t", "partial source rejects");
	Reject("0,0\t1\t0000000000011D8\t773\t", "short adapter rejects");
	Reject("0,0\t1\t00000000000011D85\t773\t", "long adapter rejects");
	Reject("0,0\t1\t0000000000011DG5\t773\t", "invalid hex rejects");
	Reject("0,0\t1\t0x00000000011D85\t773\t", "hex prefix rejects");
	Reject("0,0\t1\t0000000000011D85\t-1\t", "negative target rejects");
	Reject("0,0\t1\t0000000000011D85\t4294967295\t", "unresolved target sentinel rejects");
	Reject("0,0\t1\t0000000000011D85\t4294967296\t", "target overflow rejects");
	Reject("0,0\t1\t0000000000011D85\t773x\t", "partial target rejects");
	Reject("0,0\t1\t0000000000011D85\t773 \t", "target whitespace rejects");

	MonitorManifestRow rows[] = {
		{ -1920, 0, { UINT64_C(0xAAA), 0, 773 } },
		{ 0, 0, { UINT64_C(0xBBB), 1, 773 } }
	};
	LutDisplayIdentity identity = { 7, 8, 9 };
	Expect(FindManifestDisplayIdentity(rows, 2, -1920, 0, &identity), "find unique staged position");
	Expect(identity.adapterLuid == 0xAAA && identity.sourceId == 0 && identity.targetId == 773,
		"position association preserves canonical adapter target identity");
	Expect(!FindManifestDisplayIdentity(rows, 2, 10, 0, &identity), "unknown position rejects");
	Expect(!FindManifestDisplayIdentity(nullptr, 2, 0, 0, &identity), "null rows reject");
	Expect(!FindManifestDisplayIdentity(rows, 0, 0, 0, &identity), "empty row table rejects");
	Expect(!FindManifestDisplayIdentity(rows, -1, 0, 0, &identity), "negative row count rejects");
	Expect(!FindManifestDisplayIdentity(rows, 2, 0, 0, nullptr), "null identity output rejects");
	MonitorManifestRow duplicate[] = { rows[0], rows[0] };
	identity = { 7, 8, 9 };
	Expect(!FindManifestDisplayIdentity(duplicate, 2, -1920, 0, &identity), "duplicate identical rows are ambiguous");
	Expect(identity.adapterLuid == 7 && identity.sourceId == 8 && identity.targetId == 9,
		"failed lookup preserves output");
	duplicate[1].displayIdentity.adapterLuid = 0xBBB;
	Expect(!FindManifestDisplayIdentity(duplicate, 2, -1920, 0, &identity), "different outputs at same position reject");
	rows[0].displayIdentity.targetId = UINT32_MAX;
	Expect(!FindManifestDisplayIdentity(rows, 2, -1920, 0, &identity), "unresolved identity row rejects");

	printf("MonitorManifestTests: %d checks, %d failures\n", checks, failures);
	return failures == 0 ? 0 : 1;
}

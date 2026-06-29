
#include "pch.h"

#include <io.h>
#include <string>
#include <sstream>
#include <iostream>
#include <iomanip>
#include "DwmProfiles.generated.h"
#pragma comment (lib, "d3d11.lib")
#pragma comment (lib, "d3dcompiler.lib")
#pragma comment (lib, "dxgi.lib")
#pragma comment (lib, "uuid.lib")
#pragma comment (lib, "dxguid.lib")


#pragma intrinsic(_ReturnAddress)

#define DITHER_GAMMA 2.2
#define LUT_FOLDER "%SYSTEMROOT%\\Temp\\luts"

#define RELEASE_IF_NOT_NULL(x) { if (x != NULL) { x->Release(); } }
#define _STRINGIFY(x) #x
#define STRINGIFY(x) _STRINGIFY(x)
#define LOG_FILE_PATH "%SYSTEMROOT%\\Temp\\dwm_lut.log"
#define MAX_LOG_FILE_SIZE 20 * 1024 * 1024
#define DEBUG_MODE true

#if DEBUG_MODE == true
#define __LOG_ONLY_ONCE(x, y) if (static bool first_log_##y = true) { log_to_file(x); first_log_##y = false; }
#define _LOG_ONLY_ONCE(x, y) __LOG_ONLY_ONCE(x, y)
#define LOG_ONLY_ONCE(x) _LOG_ONLY_ONCE(x, __COUNTER__)
#define MESSAGE_BOX_DBG(x, y) MessageBoxA(NULL, x, "DEBUG HOOK DWM", y);

#define EXECUTE_WITH_LOG(winapi_func_hr) \
	do { \
		HRESULT hr = (winapi_func_hr); \
		if (FAILED(hr)) \
		{ \
			std::stringstream ss; \
			ss << "ERROR AT LINE: " << __LINE__ << " HR: " << hr << " - DETAILS: "; \
			LPSTR error_message = nullptr; \
			FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, \
				NULL, hr, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&error_message, 0, NULL); \
			ss << error_message; \
			log_to_file(ss.str().c_str()); \
			LocalFree(error_message); \
			throw std::exception(ss.str().c_str()); \
		} \
	} while (false);

#define EXECUTE_D3DCOMPILE_WITH_LOG(winapi_func_hr, error_interface) \
	do { \
		HRESULT hr = (winapi_func_hr); \
		if (FAILED(hr)) \
		{ \
			std::stringstream ss; \
			ss << "ERROR AT LINE: " << __LINE__ << " HR: " << hr << " - DETAILS: "; \
			LPSTR error_message = nullptr; \
			FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, \
				NULL, hr, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&error_message, 0, NULL); \
			ss << error_message << " - DX COMPILE ERROR: " << (char*)error_interface->GetBufferPointer(); \
			error_interface->Release(); \
			log_to_file(ss.str().c_str()); \
			LocalFree(error_message); \
			throw std::exception(ss.str().c_str()); \
		} \
	} while (false);

#define LOG_ADDRESS(prefix_message, address) \
	{ \
		std::stringstream ss; \
		ss << prefix_message << " 0x" << std::setw(sizeof(address) * 2) << std::setfill('0') << std::hex << (UINT_PTR)address; \
		log_to_file(ss.str().c_str()); \
	}

#else
#define LOG_ONLY_ONCE(x)
#define MESSAGE_BOX_DBG(x, y)
#define EXECUTE_WITH_LOG(winapi_func_hr) winapi_func_hr;
#define EXECUTE_D3DCOMPILE_WITH_LOG(winapi_func_hr, error_interface) winapi_func_hr;
#define LOG_ADDRESS(prefix_message, address)
#endif


#if DEBUG_MODE == true
void log_to_file(const char* log_buf)
{
	char logPath[MAX_PATH];
	ExpandEnvironmentStringsA(LOG_FILE_PATH, logPath, sizeof(logPath));
	FILE* pFile = fopen(logPath, "a");
	if (pFile == NULL)
	{
		return;
	}
	fseek(pFile, 0, SEEK_END);
	long size = ftell(pFile);
	if (size > MAX_LOG_FILE_SIZE)
	{
		if (_chsize(_fileno(pFile), 0) == -1)
		{
			fclose(pFile);
			return;
		}
	}
	fseek(pFile, 0, SEEK_END);
	SYSTEMTIME st;
	GetLocalTime(&st);
	fprintf(pFile, "%04u-%02u-%02u %02u:%02u:%02u.%03u pid=%lu tid=%lu %s\n",
		st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond,
		st.wMilliseconds, GetCurrentProcessId(), GetCurrentThreadId(), log_buf);
	fclose(pFile);
}
#endif

static int g_DynamicSwapChainOffset = -1;
struct RtvCacheEntry {
	ID3D11Texture2D* texture;
	ID3D11RenderTargetView* rtv;
};
static RtvCacheEntry g_RtvCache[16] = {0};
static int g_RtvCacheCount = 0;

#if DEBUG_MODE == true
void print_error(const char* prefix_message)
{
	DWORD errorCode = GetLastError();
	LPSTR errorMessage = nullptr;
	FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
	               nullptr, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&errorMessage, 0, nullptr);

	char message_buf[100];
	sprintf(message_buf, "%s: %s - error code: %u", prefix_message, errorMessage, errorCode);
	log_to_file(message_buf);
	return;
}
#endif


unsigned int lut_index(const unsigned int b, const unsigned int g, const unsigned int r, const unsigned int c,
                       const unsigned int lut_size)
{
	return lut_size * lut_size * 4 * b + lut_size * 4 * g + 4 * r + c;
}

#define LUT_ACCESS_INDEX(lut, b, g, r, c, lut_size) (*((float*)(lut) + lut_index(b, g, r, c, lut_size)))


void* get_relative_address(void* instruction_address, int offset, int instruction_size)
{
	int relative_offset = *(int*)((unsigned char*)instruction_address + offset);

	return (unsigned char*)instruction_address + instruction_size + relative_offset;
}


const unsigned char COverlayContext_Present_bytes[] = {
	0x48, 0x89, 0x5c, 0x24, 0x08, 0x48, 0x89, 0x74, 0x24, 0x10, 0x57, 0x48, 0x83, 0xec, 0x40, 0x48, 0x8b, 0xb1, 0x20,
	0x2c, 0x00, 0x00, 0x45, 0x8b, 0xd0, 0x48, 0x8b, 0xfa, 0x48, 0x8b, 0xd9, 0x48, 0x85, 0xf6, 0x0f, 0x85
};
const int IOverlaySwapChain_IDXGISwapChain_offset = -0x118;

const unsigned char COverlayContext_IsCandidateDirectFlipCompatbile_bytes[] = {
	0x48, 0x89, 0x7c, 0x24, 0x20, 0x55, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x8b, 0xec, 0x48, 0x83,
	0xec, 0x40
};
const unsigned char COverlayContext_OverlaysEnabled_bytes[] = {
	0x75, 0x04, 0x32, 0xc0, 0xc3, 0xcc, 0x83, 0x79, 0x30, 0x01, 0x0f, 0x97, 0xc0, 0xc3
};

const int COverlayContext_DeviceClipBox_offset = -0x120;

const int IOverlaySwapChain_HardwareProtected_offset = -0xbc;


const unsigned char COverlayContext_Present_bytes_w11[] = {
	0x40, 0x53, 0x55, 0x56, 0x57, 0x41, 0x56, 0x41, 0x57, 0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x05,
	'?', '?', '?', '?', 0x48, 0x33, 0xC4, 0x48, 0x89, 0x44, 0x24, 0x78, 0x48
};
const int IOverlaySwapChain_IDXGISwapChain_offset_w11 = 0xE0;


const unsigned char COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11[] = {
	0x40, 0x55, 0x53, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x8B, 0xEC, 0x48, 0x83, 0xEC,
	0x68, 0x48,
};


const unsigned char COverlayContext_OverlaysEnabled_bytes_w11[] = {
	0x83, 0x3D, '?', '?', '?', '?', '?', 0x75, 0x04
};

int COverlayContext_DeviceClipBox_offset_w11 = 0x466C;

const int IOverlaySwapChain_HardwareProtected_offset_w11 = -0x144;



const unsigned char COverlayContext_Present_bytes_w11_24h2[] = {
	0x4C, 0x8B, 0xDC, 0x56, 0x41, 0x56
};

const int IOverlaySwapChain_IDXGISwapChain_offset_w11_24h2 = 0x108;

const unsigned char COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_24h2[] = {
	0x48, 0x8B, 0xC4, 0x48, 0x89, 0x58, '?', 0x48, 0x89, 0x68, '?', 0x48, 0x89, 0x70, '?', 0x48, 0x89, 0x78, '?', 0x41, 0x56, 0x48, 0x83, 0xEC, 0x20, 0x33, 0xDB
};

const unsigned char COverlayContext_OverlaysEnabled_bytes_relative_w11_24h2[] = {
	0xE8, '?', '?', '?', '?', 0x84, 0xC0, 0xB8, 0x04, 0x00, 0x00, 0x00
};

int COverlayContext_DeviceClipBox_offset_w11_24h2 = 0x53E8;

const int IOverlaySwapChain_HardwareProtected_offset_w11_24h2 = 0x64;



const unsigned char COverlayContext_Present_bytes_w11_25h2[] = {
	0x40, 0x55, 0x53, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x8D, 0x6C,
	0x24, 0xF9, 0x48, 0x81, 0xEC, 0xF8, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x05,
	'?', '?', '?', '?', 0x48, 0x33, 0xC4, 0x48, 0x89, 0x45, 0xEF, 0x4C, 0x8B, 0x65, '?', 0x48, 0x8B, 0xD9
};


const unsigned char COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_25h2[] = {
	0x48, 0x8B, 0xC4, 0x48, 0x89, 0x58, 0x08, 0x48, 0x89, 0x68, 0x10, 0x48, 0x89, 0x70, 0x18, 0x48,
	0x89, 0x78, 0x20, 0x41, 0x56, 0x48, 0x83, 0xEC, 0x20, 0x33, 0xDB
};


const unsigned char COverlayContext_OverlaysEnabled_bytes_w11_25h2[] = {
	0x83, 0x3D, '?', '?', '?', '?', 0x05, 0x74, 0x09, 0x83, 0x79, 0x28, 0x01, 0x0F, 0x97, 0xC0, 0xC3
};


const unsigned char CWindowContext_IsCandidateDirectFlipCompatible_bytes_w11_25h2[] = {
	0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74, 0x24, 0x10, 0x57, 0x48, 0x83, 0xEC, 0x20, 0x41, 0x8B, 0xD9, 0x48, 0x8B, 0xF2, 0x4C, 0x8B, 0x01, 0x48, 0x8B, 0xF9
};


const unsigned char CCompSwapChain_IsCandidateDirectFlipCompatible_bytes_w11_25h2[] = {
	0x48, 0x8B, 0xC4, 0x48, 0x89, 0x58, 0x08, 0x48, 0x89, 0x68, 0x10, 0x48, 0x89, 0x70, 0x18, 0x48, 0x89, 0x78, 0x20, 0x41, 0x56, 0x48, 0x83, 0xEC, 0x20, 0x33, 0xDB, 0x41, 0x8B, 0xF0
};


const unsigned char CCompVisual_IsCandidateForPromotion_bytes_w11_25h2[] = {
	0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x57, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x8B, 0x01, 0x41, 0x8B, 0xD1, 0x48, 0x8B, 0xF1
};


int COverlayContext_DeviceClipBox_offset_w11_25h2 = 0x7698;

const int IOverlaySwapChain_HardwareProtected_offset_w11_25h2 = 0x4C;


const int IOverlaySwapChain_GetSwapChain_vtable_offset_w11_25h2 = 0x108;


bool isWindows11 = false;
bool isWindows11_24h2 = false;
bool isWindows11_25h2 = false;


static int* g_pOverlayTestMode = NULL;
static int g_previousOverlayTestMode = 0;
static bool g_hasPreviousOverlayTestMode = false;
static const DwmSymbolProfile* g_activeDwmProfile = NULL;

enum ResolverMode
{
	ResolverAuto,
	ResolverExactProfileOnly,
	ResolverLegacyWin10Signatures,
	ResolverLegacyWin11Signatures,
	Resolver24H2Signatures,
	Resolver25H2Signatures
};

static ResolverMode g_resolverMode = ResolverAuto;

const char* ResolverModeName(ResolverMode mode)
{
	switch (mode)
	{
	case ResolverExactProfileOnly: return "exact-profile-only";
	case ResolverLegacyWin10Signatures: return "legacy-win10-signatures";
	case ResolverLegacyWin11Signatures: return "legacy-win11-signatures";
	case Resolver24H2Signatures: return "24h2-signatures";
	case Resolver25H2Signatures: return "25h2-signatures";
	default: return "auto";
	}
}

ResolverMode ParseResolverMode(const char* value)
{
	if (value == NULL) return ResolverAuto;
	if (_stricmp(value, "exact-profile-only") == 0) return ResolverExactProfileOnly;
	if (_stricmp(value, "legacy-win10-signatures") == 0) return ResolverLegacyWin10Signatures;
	if (_stricmp(value, "legacy-win11-signatures") == 0) return ResolverLegacyWin11Signatures;
	if (_stricmp(value, "24h2-signatures") == 0) return Resolver24H2Signatures;
	if (_stricmp(value, "25h2-signatures") == 0) return Resolver25H2Signatures;
	return ResolverAuto;
}

bool ResolverAllowsExactProfile()
{
	return g_resolverMode == ResolverAuto || g_resolverMode == ResolverExactProfileOnly;
}

void ReadResolverConfig(const char* folder)
{
	char path[MAX_PATH];
	if (sprintf_s(path, "%s\\resolver.cfg", folder) < 0)
	{
		log_to_file("Resolver config path is too long; using auto resolver");
		g_resolverMode = ResolverAuto;
		return;
	}

	FILE* file = fopen(path, "r");
	if (file == NULL)
	{
		log_to_file("No resolver.cfg staged; using auto resolver");
		g_resolverMode = ResolverAuto;
		return;
	}

	char line[128] = {};
	if (fgets(line, sizeof(line), file) != NULL)
	{
		line[strcspn(line, "\r\n")] = '\0';
		const char* value = line;
		if (_strnicmp(value, "mode=", 5) == 0)
		{
			value += 5;
		}
		g_resolverMode = ParseResolverMode(value);
	}
	fclose(file);

	std::stringstream ss;
	ss << "Resolver mode: " << ResolverModeName(g_resolverMode);
	log_to_file(ss.str().c_str());
}

void ApplyResolverModeToVersionFlags()
{
	switch (g_resolverMode)
	{
	case ResolverLegacyWin10Signatures:
		isWindows11 = false;
		isWindows11_24h2 = false;
		isWindows11_25h2 = false;
		break;
	case ResolverLegacyWin11Signatures:
		isWindows11 = true;
		isWindows11_24h2 = false;
		isWindows11_25h2 = false;
		break;
	case Resolver24H2Signatures:
		isWindows11 = true;
		isWindows11_24h2 = true;
		isWindows11_25h2 = false;
		break;
	case Resolver25H2Signatures:
		isWindows11 = true;
		isWindows11_24h2 = false;
		isWindows11_25h2 = true;
		break;
	default:
		break;
	}

	std::stringstream ss;
	ss << "Resolver version flags: win11=" << (isWindows11 ? 1 : 0)
		<< " 24h2=" << (isWindows11_24h2 ? 1 : 0)
		<< " 25h2plus=" << (isWindows11_25h2 ? 1 : 0);
	log_to_file(ss.str().c_str());
}

bool aob_match_inverse(const void* buf1, const void* mask, const int buf_len)
{
	for (int i = 0; i < buf_len; ++i)
	{
		if (((unsigned char*)buf1)[i] != ((unsigned char*)mask)[i] && ((unsigned char*)mask)[i] != '?')
		{
			return true;
		}
	}
	return false;
}

char shaders[] = R"(
    struct VS_INPUT {
	float2 pos : POSITION;
	float2 tex : TEXCOORD;
};

struct VS_OUTPUT {
	float4 pos : SV_POSITION;
	float2 tex : TEXCOORD;
};

Texture2D backBufferTex : register(t0);
Texture3D lutTex : register(t1);
SamplerState smp : register(s0);

Texture2D noiseTex : register(t2);
SamplerState noiseSmp : register(s1);

cbuffer ConstantBuffer : register(b0) {
    int lutSize;
    bool hdr;
};

static float3x3 scrgb_to_bt2100 = {
2939026994.L / 585553224375.L, 9255011753.L / 3513319346250.L,   173911579.L / 501902763750.L,
  76515593.L / 138420033750.L, 6109575001.L / 830520202500.L,    75493061.L / 830520202500.L,
  12225392.L / 93230009375.L, 1772384008.L / 2517210253125.L, 18035212433.L / 2517210253125.L,
};

static float3x3 bt2100_to_scrgb = {
 348196442125.L / 1677558947.L, -123225331250.L / 1677558947.L,  -15276242500.L / 1677558947.L,
-579752563250.L / 37238079773.L, 5273377093000.L / 37238079773.L,  -38864558125.L / 37238079773.L,
 -12183628000.L / 5369968309.L, -472592308000.L / 37589778163.L, 5256599974375.L / 37589778163.L,
};

static float m1 = 1305 / 8192.;
static float m2 = 2523 / 32.;
static float c1 = 107 / 128.;
static float c2 = 2413 / 128.;
static float c3 = 2392 / 128.;

float3 SampleLut(float3 index) {
	float3 tex = (index + 0.5) / lutSize;
	return lutTex.Sample(smp, tex).rgb;
}


void barycentricWeight(float3 r, out float4 bary, out int3 vert2, out int3 vert3) {
	vert2 = int3(0, 0, 0); vert3 = int3(1, 1, 1);
	int3 c = r.xyz >= r.yzx;
	bool c_xy = c.x; bool c_yz = c.y; bool c_zx = c.z;
	bool c_yx = !c.x; bool c_zy = !c.y; bool c_xz = !c.z;
	bool cond;  float3 s = float3(0, 0, 0);
#define ORDER(X, Y, Z)                   \
            cond = c_ ## X ## Y && c_ ## Y ## Z; \
            s = cond ? r.X ## Y ## Z : s;        \
            vert2.X = cond ? 1 : vert2.X;        \
            vert3.Z = cond ? 0 : vert3.Z;
	ORDER(x, y, z)   ORDER(x, z, y)   ORDER(z, x, y)
		ORDER(z, y, x)   ORDER(y, z, x)   ORDER(y, x, z)
		bary = float4(1 - s.x, s.z, s.x - s.y, s.y - s.z);
}

float3 LutTransformTetrahedral(float3 rgb) {
	float3 lutIndex = rgb * (lutSize - 1);
	float4 bary; int3 vert2; int3 vert3;
	barycentricWeight(frac(lutIndex), bary, vert2, vert3);

	float3 base = floor(lutIndex);
	return bary.x * SampleLut(base) +
		bary.y * SampleLut(base + 1) +
		bary.z * SampleLut(base + vert2) +
		bary.w * SampleLut(base + vert3);
}

float3 pq_eotf(float3 e) {
	return pow(max((pow(e, 1 / m2) - c1), 0) / (c2 - c3 * pow(e, 1 / m2)), 1 / m1);
}

float3 pq_inv_eotf(float3 y) {
	return pow((c1 + c2 * pow(y, m1)) / (1 + c3 * pow(y, m1)), m2);
}

float3 OrderedDither(float3 rgb, float2 pos) {
	float3 low = floor(rgb * 255) / 255;
	float3 high = low + 1.0 / 255;

	float3 rgb_linear = pow(rgb,)" STRINGIFY(DITHER_GAMMA) R"();
	float3 low_linear = pow(low,)" STRINGIFY(DITHER_GAMMA) R"();
	float3 high_linear = pow(high,)" STRINGIFY(DITHER_GAMMA) R"();

	float noise = noiseTex.Sample(noiseSmp, pos / )" STRINGIFY(NOISE_SIZE) R"().x;
	float3 threshold = lerp(low_linear, high_linear, noise);

	return lerp(low, high, rgb_linear > threshold);
}

VS_OUTPUT VS(VS_INPUT input) {
	VS_OUTPUT output;
	output.pos = float4(input.pos, 0, 1);
	output.tex = input.tex;
	return output;
}

float4 PS(VS_OUTPUT input) : SV_TARGET{
	float3 sample = backBufferTex.Sample(smp, input.tex).rgb;

	if (hdr) {
		float3 hdr10_sample = pq_inv_eotf(saturate(mul(scrgb_to_bt2100, sample)));

		float3 hdr10_res = LutTransformTetrahedral(hdr10_sample);

		float3 scrgb_res = mul(bt2100_to_scrgb, pq_eotf(hdr10_res));

		return float4(scrgb_res, 1);
	}
	else {
		float3 res = LutTransformTetrahedral(sample);

		res = OrderedDither(res, input.pos.xy);

		return float4(res, 1);
	}
}
)";

ID3D11Device* device;
ID3D11DeviceContext* deviceContext;
ID3D11VertexShader* vertexShader;
ID3D11PixelShader* pixelShader;
ID3D11InputLayout* inputLayout;

ID3D11Buffer* vertexBuffer;
UINT numVerts;
UINT stride;
UINT offset;

D3D11_TEXTURE2D_DESC backBufferDesc;
D3D11_TEXTURE2D_DESC textureDesc[2];

ID3D11SamplerState* samplerState;
ID3D11Texture2D* texture[2];
ID3D11ShaderResourceView* textureView[2];

ID3D11SamplerState* noiseSamplerState;
ID3D11ShaderResourceView* noiseTextureView;

ID3D11Buffer* constantBuffer;

struct lutData
{
	int left;
	int top;
	int size;
	bool isHdr;
	ID3D11ShaderResourceView* textureView;
	float* rawLut;
	char fileName[MAX_PATH];
};

void DrawRectangle(struct tagRECT* rect, int index)
{
	float width = static_cast<float>(backBufferDesc.Width);
	float height = static_cast<float>(backBufferDesc.Height);

	float screenLeft = rect->left / width;
	float screenTop = rect->top / height;
	float screenRight = rect->right / width;
	float screenBottom = rect->bottom / height;

	float left = screenLeft * 2 - 1;
	float top = screenTop * -2 + 1;
	float right = screenRight * 2 - 1;
	float bottom = screenBottom * -2 + 1;

	width = static_cast<float>(textureDesc[index].Width);
	height = static_cast<float>(textureDesc[index].Height);
	float texLeft = rect->left / width;
	float texTop = rect->top / height;
	float texRight = rect->right / width;
	float texBottom = rect->bottom / height;

	float vertexData[] = {
		left, bottom, texLeft, texBottom,
		left, top, texLeft, texTop,
		right, bottom, texRight, texBottom,
		right, top, texRight, texTop
	};

	D3D11_MAPPED_SUBRESOURCE resource;
	EXECUTE_WITH_LOG(deviceContext->Map(vertexBuffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &resource))
	memcpy(resource.pData, vertexData, stride * numVerts);
	deviceContext->Unmap(vertexBuffer, 0);

	deviceContext->IASetVertexBuffers(0, 1, &vertexBuffer, &stride, &offset);

	deviceContext->Draw(numVerts, 0);
}

int numLuts;

lutData* luts;

bool TryGetSingleMonitorLUT(bool hdr, lutData** lut)
{
	if (numLuts <= 0 || luts == NULL)
	{
		return false;
	}

	int left = luts[0].left;
	int top = luts[0].top;
	for (int i = 1; i < numLuts; i++)
	{
		if (luts[i].left != left || luts[i].top != top)
		{
			return false;
		}
	}

	for (int i = 0; i < numLuts; i++)
	{
		if (luts[i].isHdr == hdr)
		{
			LOG_ONLY_ONCE("Single-monitor fast path selected matching SDR/HDR LUT")
			*lut = &luts[i];
			return true;
		}
	}

	LOG_ONLY_ONCE("Single-monitor fast path fell back to first available LUT")
	*lut = &luts[0];
	return true;
}

lutData* FindNearestLUTByPosition(int left, int top, bool hdr)
{
	// DWM sometimes reports a monitor rectangle that is a few pixels away from
	// the GUI's display topology; keep this fallback deliberately tight so a
	// multi-monitor setup does not silently receive the wrong calibration.
	lutData* best = NULL;
	long long bestDistance = 0x7fffffffffffffffLL;

	for (int i = 0; i < numLuts; i++)
	{
		if (luts[i].isHdr != hdr)
		{
			continue;
		}

		long long dx = (long long)luts[i].left - left;
		long long dy = (long long)luts[i].top - top;
		long long distance = dx * dx + dy * dy;
		if (distance < bestDistance)
		{
			bestDistance = distance;
			best = &luts[i];
		}
	}

	if (best != NULL && bestDistance <= 64 * 64)
	{
		char message_buf[256];
		sprintf_s(message_buf, "Nearest multi-monitor LUT selected for DWM position %d,%d -> staged %d,%d hdr=%d distance2=%lld",
			left, top, best->left, best->top, hdr ? 1 : 0, bestDistance);
		LOG_ONLY_ONCE(message_buf)
		return best;
	}

	return NULL;
}

static bool IsCubeNumericLine(const char* line)
{
	while (*line == ' ' || *line == '\t')
	{
		line++;
	}

	return (*line >= '0' && *line <= '9') || *line == '-' || *line == '+' || *line == '.';
}

static void LogLutParseFailure(const char* filename, const char* reason)
{
	char message_buf[512];
	sprintf_s(message_buf, "LUT parse failed for %s: %s", filename, reason);
	log_to_file(message_buf);
}

static bool IsSupportedLutSize(unsigned int lutSize)
{
	// Common calibration LUTs are 17, 33, or 65. Keep a generous upper bound,
	// but refuse pathological sizes before allocating inside dwm.exe.
	return lutSize >= 2 && lutSize <= 129;
}

bool ParseLUT(lutData* lut, const char* filename)
{
	FILE* file = fopen(filename, "r");
	if (file == NULL)
	{
		LogLutParseFailure(filename, "file could not be opened");
		return false;
	}

	char line[256];
	unsigned int lutSize = 0;

	while (1)
	{
		if (!fgets(line, sizeof(line), file))
		{
			fclose(file);
			LogLutParseFailure(filename, "missing LUT_3D_SIZE header");
			return false;
		}
		if (sscanf_s(line, " LUT_3D_SIZE %u", &lutSize) == 1)
		{
			break;
		}
	}

	if (!IsSupportedLutSize(lutSize))
	{
		fclose(file);
		LogLutParseFailure(filename, "unsupported LUT_3D_SIZE");
		return false;
	}

	size_t lutEntries = (size_t)lutSize * (size_t)lutSize * (size_t)lutSize;
	if (lutEntries > SIZE_MAX / (4 * sizeof(float)))
	{
		fclose(file);
		LogLutParseFailure(filename, "LUT allocation size overflow");
		return false;
	}

	float* rawLut = (float*)malloc(lutEntries * 4 * sizeof(float));
	if (rawLut == NULL)
	{
		fclose(file);
		LogLutParseFailure(filename, "memory allocation failed");
		return false;
	}

	for (unsigned int b = 0; b < lutSize; b++)
	{
		for (unsigned int g = 0; g < lutSize; g++)
		{
			for (unsigned int r = 0; r < lutSize; r++)
			{
				while (1)
				{
					if (!fgets(line, sizeof(line), file))
					{
						fclose(file);
						free(rawLut);
						LogLutParseFailure(filename, "file ended before all LUT entries were read");
						return false;
					}

					if (!IsCubeNumericLine(line))
					{
						continue;
					}

					float red, green, blue;

					if (sscanf_s(line, "%f%f%f", &red, &green, &blue) != 3)
					{
						fclose(file);
						free(rawLut);
						LogLutParseFailure(filename, "numeric LUT row did not contain three floats");
						return false;
					}
					LUT_ACCESS_INDEX(rawLut, b, g, r, 0, lutSize) = red;
					LUT_ACCESS_INDEX(rawLut, b, g, r, 1, lutSize) = green;
					LUT_ACCESS_INDEX(rawLut, b, g, r, 2, lutSize) = blue;
					LUT_ACCESS_INDEX(rawLut, b, g, r, 3, lutSize) = 1;

					break;
				}
			}
		}
	}
	fclose(file);
	lut->size = lutSize;
	lut->rawLut = rawLut;
	return true;
}

void LogMonitorManifest(char* folder)
{
	// The GUI deletes the staging directory after LoadLibrary returns. Copy the
	// GUI's topology view into the durable DWM log while the manifest exists.
	char manifestPath[MAX_PATH];
	strcpy_s(manifestPath, folder);
	strcat_s(manifestPath, "\\manifest.tsv");

	FILE* file = fopen(manifestPath, "r");
	if (file == NULL)
	{
		LOG_ONLY_ONCE("No staged monitor manifest.tsv was found")
		return;
	}

	log_to_file("Begin staged monitor manifest.tsv");
	char line[1024];
	int lineCount = 0;
	while (fgets(line, sizeof(line), file) != NULL && lineCount < 64)
	{
		line[strcspn(line, "\r\n")] = '\0';
		log_to_file(line);
		lineCount++;
	}
	fclose(file);
	log_to_file("End staged monitor manifest.tsv");
}

bool AddLUTs(char* folder)
{
	LogMonitorManifest(folder);

	WIN32_FIND_DATAA findData;

	char path[MAX_PATH];
	if (sprintf_s(path, "%s\\*", folder) < 0)
	{
		log_to_file("LUT folder path is too long");
		return false;
	}
	HANDLE hFind = FindFirstFileA(path, &findData);
	if (hFind == INVALID_HANDLE_VALUE) return false;
	do
	{
		if (!(findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
		{
			char filePath[MAX_PATH];
			char* fileName = findData.cFileName;

			if (sprintf_s(filePath, "%s\\%s", folder, fileName) < 0)
			{
				log_to_file("Staged LUT file path is too long");
				FindClose(hFind);
				return false;
			}

			lutData candidate = {};
			if (sscanf_s(findData.cFileName, "%d_%d", &candidate.left, &candidate.top) == 2)
			{
				candidate.isHdr = strstr(fileName, "hdr") != NULL;
				candidate.textureView = NULL;
				strcpy_s(candidate.fileName, fileName);
				if (!ParseLUT(&candidate, filePath))
				{
					FindClose(hFind);
					return false;
				}

				lutData* resized = (lutData*)realloc(luts, (numLuts + 1) * sizeof(*luts));
				if (resized == NULL)
				{
					free(candidate.rawLut);
					log_to_file("Could not grow LUT table");
					FindClose(hFind);
					return false;
				}

				luts = resized;
				luts[numLuts] = candidate;
				char message_buf[256];
				sprintf_s(message_buf, "Loaded LUT file=%s position=%d,%d hdr=%d size=%d",
					candidate.fileName, candidate.left, candidate.top, candidate.isHdr ? 1 : 0, candidate.size);
				log_to_file(message_buf);
				numLuts++;
			}
			else if (_stricmp(fileName, "manifest.tsv") == 0)
			{
				log_to_file("Detected staged monitor manifest.tsv");
			}
			else if (_stricmp(fileName, "resolver.cfg") == 0)
			{
				log_to_file("Detected staged resolver.cfg");
			}
		}
	}
	while (FindNextFileA(hFind, &findData) != 0);
	FindClose(hFind);
	char message_buf[100];
	sprintf_s(message_buf, "Total parsed LUT files: %d", numLuts);
	log_to_file(message_buf);
	return true;
}

int numLutTargets;
void** lutTargets;



static void* g_primaryHdrContext = NULL;

bool IsLUTActive(void* target)
{
	for (int i = 0; i < numLutTargets; i++)
	{
		if (lutTargets[i] == target)
		{
			return true;
		}
	}
	return false;
}

void SetLUTActive(void* target)
{
	if (!IsLUTActive(target))
	{
		void** resized = (void**)realloc(lutTargets, (numLutTargets + 1) * sizeof(*lutTargets));
		if (resized == NULL)
		{
			LOG_ONLY_ONCE("Could not grow active LUT target table")
			return;
		}
		lutTargets = resized;
		lutTargets[numLutTargets++] = target;
	}
}

void UnsetLUTActive(void* target)
{
	for (int i = 0; i < numLutTargets; i++)
	{
		if (lutTargets[i] == target)
		{
			lutTargets[i] = lutTargets[--numLutTargets];
			if (numLutTargets == 0)
			{
				free(lutTargets);
				lutTargets = NULL;
			}
			else
			{
				void** resized = (void**)realloc(lutTargets, numLutTargets * sizeof(*lutTargets));
				if (resized != NULL)
				{
					lutTargets = resized;
				}
			}
			return;
		}
	}
}

lutData* GetLUTDataFromCOverlayContext(void* context, bool hdr)
{
	static void* last_context = NULL;
	static lutData* last_lut = NULL;
	static bool last_hdr = false;

	if (context == last_context && last_lut != NULL && last_hdr == hdr)
	{
		return last_lut;
	}

	lutData* singleMonitorLut = NULL;
	if (TryGetSingleMonitorLUT(hdr, &singleMonitorLut))
	{
		last_context = context;
		last_lut = singleMonitorLut;
		last_hdr = hdr;
		return singleMonitorLut;
	}

	int left, top;

	if (isWindows11_25h2)
	{
		void* realObj = *(void**)context;


		int* rect = (int*)((unsigned char*)realObj + 0x4D0);

		left = (int)rect[0];
		top = (int)rect[1];



		if ((left == 0 && top == 0) || (left < -2000 || left > 10000)) {
			left = 0;
			top = 0;
		}
	}
	else if (isWindows11_24h2)
	{
		float* rect = (float*)((unsigned char*)*(void**)context + COverlayContext_DeviceClipBox_offset_w11_24h2);
		left = (int)rect[2];
		top = (int)rect[3];
		char message_buf[100];
		sprintf(message_buf, "Left: %d, Top: %d", left, top);
		LOG_ONLY_ONCE(message_buf)


		sprintf(message_buf, "Rect address: 0x%p", rect);
		LOG_ONLY_ONCE(message_buf)
	}
	else if (isWindows11)
	{
		float* rect = (float*)((unsigned char*)*(void**)context + COverlayContext_DeviceClipBox_offset_w11);
		left = (int)rect[0];
		top = (int)rect[1];
		char message_buf[100];
		sprintf(message_buf, "Left: %d, Top: %d", left, top);
		LOG_ONLY_ONCE(message_buf)


		sprintf(message_buf, "Rect address: 0x%p", rect);
		LOG_ONLY_ONCE(message_buf)
	}
	else
	{
		int* rect = (int*)((unsigned char*)context + COverlayContext_DeviceClipBox_offset);
		left = rect[0];
		top = rect[1];
	}

	for (int i = 0; i < numLuts; i++)
	{
		if (luts[i].left == left && luts[i].top == top && luts[i].isHdr == hdr)
		{
			char message_buf[256];
			sprintf_s(message_buf, "Exact multi-monitor LUT match for DWM position %d,%d hdr=%d file=%s",
				left, top, hdr ? 1 : 0, luts[i].fileName);
			LOG_ONLY_ONCE(message_buf)
			last_context = context;
			last_lut = &luts[i];
			last_hdr = hdr;
			return &luts[i];
		}
	}


	lutData* nearest = FindNearestLUTByPosition(left, top, hdr);
	if (nearest != NULL)
	{
		last_context = context;
		last_lut = nearest;
		last_hdr = hdr;
		return nearest;
	}

	if (isWindows11_25h2 && g_primaryHdrContext == context)
	{
		for (int i = 0; i < numLuts; i++)
		{
			if (luts[i].left == left && luts[i].top == top && luts[i].isHdr != hdr)
			{
				LOG_ONLY_ONCE("Using primary HDR context fallback with opposite HDR flag")
				return &luts[i];
			}
		}
	}



	for (int i = 0; i < numLuts; i++)
	{
		if (luts[i].isHdr == hdr)
		{
			LOG_ONLY_ONCE("Falling back to first LUT with matching HDR state")
			return &luts[i];
		}
	}


	if (numLuts > 0) 
	{
		LOG_ONLY_ONCE("Falling back to first loaded LUT because no position/HDR match was available")
		last_context = context;
		last_lut = &luts[0];
		last_hdr = hdr;
		return &luts[0];
	}

	return NULL;
}

void InitializeStuff(ID3D11Device* inputDevice)
{
	try
	{
		device = inputDevice;
		device->AddRef();
		LOG_ONLY_ONCE("Device successfully gathered")
		LOG_ADDRESS("The device address is: ", device)

		device->GetImmediateContext(&deviceContext);
		LOG_ONLY_ONCE("Got context after device")
		LOG_ADDRESS("The Device context is located at address: ", deviceContext)
		{
			ID3DBlob* vsBlob;
			ID3DBlob* compile_error_interface;
			LOG_ONLY_ONCE(("Trying to compile vshader with this code:\n" + std::string(shaders)).c_str())
			EXECUTE_D3DCOMPILE_WITH_LOG(
				D3DCompile(shaders, sizeof shaders, NULL, NULL, NULL, "VS", "vs_5_0", 0, 0, &vsBlob, &
					compile_error_interface), compile_error_interface)


			LOG_ONLY_ONCE("Vertex shader compiled successfully")
			EXECUTE_WITH_LOG(device->CreateVertexShader(vsBlob->GetBufferPointer(),
				vsBlob->GetBufferSize(), NULL, &vertexShader))


			LOG_ONLY_ONCE("Vertex shader created successfully")
			D3D11_INPUT_ELEMENT_DESC inputElementDesc[] =
			{
				{"POSITION", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0},
				{
					"TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, D3D11_APPEND_ALIGNED_ELEMENT,
					D3D11_INPUT_PER_VERTEX_DATA, 0
				}
			};
			EXECUTE_WITH_LOG(device->CreateInputLayout(inputElementDesc, ARRAYSIZE(inputElementDesc),
				vsBlob->GetBufferPointer(),
				vsBlob->GetBufferSize(), &inputLayout))

			vsBlob->Release();
		}
		{
			ID3DBlob* psBlob;
			ID3DBlob* compile_error_interface;
			EXECUTE_D3DCOMPILE_WITH_LOG(
				D3DCompile(shaders, sizeof shaders, NULL, NULL, NULL, "PS", "ps_5_0", 0, 0, &psBlob, &
					compile_error_interface), compile_error_interface)

			LOG_ONLY_ONCE("Pixel shader compiled successfully")
			device->CreatePixelShader(psBlob->GetBufferPointer(),
			                          psBlob->GetBufferSize(), NULL, &pixelShader);
			psBlob->Release();
		}
		{
			stride = 4 * sizeof(float);
			numVerts = 4;
			offset = 0;

			D3D11_BUFFER_DESC vertexBufferDesc = {};
			vertexBufferDesc.ByteWidth = stride * numVerts;
			vertexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
			vertexBufferDesc.BindFlags = D3D11_BIND_VERTEX_BUFFER;
			vertexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

			EXECUTE_WITH_LOG(device->CreateBuffer(&vertexBufferDesc, NULL, &vertexBuffer))
		}
		{
			D3D11_SAMPLER_DESC samplerDesc = {};
			samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_POINT;
			samplerDesc.AddressU = samplerDesc.AddressV = samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
			samplerDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;

			EXECUTE_WITH_LOG(device->CreateSamplerState(&samplerDesc, &samplerState))
		}
		for (int i = 0; i < numLuts; i++)
		{
			lutData* lut = &luts[i];

			D3D11_TEXTURE3D_DESC desc = {};
			desc.Width = lut->size;
			desc.Height = lut->size;
			desc.Depth = lut->size;
			desc.MipLevels = 1;
			desc.Format = DXGI_FORMAT_R32G32B32A32_FLOAT;
			desc.Usage = D3D11_USAGE_IMMUTABLE;
			desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

			D3D11_SUBRESOURCE_DATA initData;
			initData.pSysMem = lut->rawLut;
			initData.SysMemPitch = lut->size * 4 * sizeof(float);
			initData.SysMemSlicePitch = lut->size * lut->size * 4 * sizeof(float);

			ID3D11Texture3D* tex;
			EXECUTE_WITH_LOG(device->CreateTexture3D(&desc, &initData, &tex))
			EXECUTE_WITH_LOG(device->CreateShaderResourceView((ID3D11Resource*)tex, NULL, &luts[i].textureView))
			tex->Release();
			free(lut->rawLut);
			lut->rawLut = NULL;
		}
		{
			D3D11_SAMPLER_DESC samplerDesc = {};
			samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_POINT;
			samplerDesc.AddressU = samplerDesc.AddressV = samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
			samplerDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;

			EXECUTE_WITH_LOG(device->CreateSamplerState(&samplerDesc, &noiseSamplerState))
		}
		{
			D3D11_TEXTURE2D_DESC desc = {};
			desc.Width = NOISE_SIZE;
			desc.Height = NOISE_SIZE;
			desc.MipLevels = 1;
			desc.ArraySize = 1;
			desc.Format = DXGI_FORMAT_R32_FLOAT;
			desc.SampleDesc.Count = 1;
			desc.SampleDesc.Quality = 0;
			desc.Usage = D3D11_USAGE_IMMUTABLE;
			desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

			float noise[NOISE_SIZE][NOISE_SIZE];

			for (int i = 0; i < NOISE_SIZE; i++)
			{
				for (int j = 0; j < NOISE_SIZE; j++)
				{
					noise[i][j] = (static_cast<float>(noiseBytes[i][j]) + 0.5f) / 256.0f;
				}
			}

			D3D11_SUBRESOURCE_DATA initData;
			initData.pSysMem = noise;
			initData.SysMemPitch = sizeof(noise[0]);

			ID3D11Texture2D* tex;
			EXECUTE_WITH_LOG(device->CreateTexture2D(&desc, &initData, &tex))
			EXECUTE_WITH_LOG(device->CreateShaderResourceView((ID3D11Resource*)tex, NULL, &noiseTextureView))
			tex->Release();
		}
		{
			D3D11_BUFFER_DESC constantBufferDesc = {};
			constantBufferDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
			constantBufferDesc.ByteWidth = 16;
			constantBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
			constantBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

			EXECUTE_WITH_LOG(device->CreateBuffer(&constantBufferDesc, NULL, &constantBuffer))
			LOG_ONLY_ONCE("Final buffer created in InitializeStuff")
		}
	}
	catch (std::exception& ex)
	{
		std::stringstream ex_message;
		ex_message << "Exception caught at line " << __LINE__ << ": " << ex.what() << std::endl;
		LOG_ONLY_ONCE(ex_message.str().c_str())
		throw;
	}
	catch (...)
	{
		std::stringstream ex_message;
		ex_message << "Exception caught at line " << __LINE__ << ": " << std::endl;
		LOG_ONLY_ONCE(ex_message.str().c_str())
		throw;
	}
}

void UninitializeStuff()
{
	RELEASE_IF_NOT_NULL(device)
	RELEASE_IF_NOT_NULL(deviceContext)
	RELEASE_IF_NOT_NULL(vertexShader)
	RELEASE_IF_NOT_NULL(pixelShader)
	RELEASE_IF_NOT_NULL(inputLayout)
	RELEASE_IF_NOT_NULL(vertexBuffer)
	RELEASE_IF_NOT_NULL(samplerState)
	for (int i = 0; i < 2; i++)
	{
		RELEASE_IF_NOT_NULL(texture[i])
		RELEASE_IF_NOT_NULL(textureView[i])
	}
	for (int i = 0; i < g_RtvCacheCount; i++)
	{
		RELEASE_IF_NOT_NULL(g_RtvCache[i].rtv)
	}
	g_RtvCacheCount = 0;
	RELEASE_IF_NOT_NULL(noiseSamplerState)
	RELEASE_IF_NOT_NULL(noiseTextureView)
	RELEASE_IF_NOT_NULL(constantBuffer)
	for (int i = 0; i < numLuts; i++)
	{
		free(luts[i].rawLut);
		RELEASE_IF_NOT_NULL(luts[i].textureView)
	}
	free(luts);
	free(lutTargets);
}


bool RenderLUT(void* cOverlayContext, ID3D11Texture2D* backBuffer, struct tagRECT* rects, int numRects)
{
	ID3D11RenderTargetView* renderTargetView;

	D3D11_TEXTURE2D_DESC newBackBufferDesc;
	backBuffer->GetDesc(&newBackBufferDesc);

	int index = -1;
	if (newBackBufferDesc.Format == DXGI_FORMAT_B8G8R8A8_UNORM ||
	    newBackBufferDesc.Format == DXGI_FORMAT_R8G8B8A8_UNORM ||
	    newBackBufferDesc.Format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB ||
	    newBackBufferDesc.Format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB ||
	    newBackBufferDesc.Format == DXGI_FORMAT_R10G10B10A2_UNORM)
	{
		index = 0;
	}
	else if (newBackBufferDesc.Format == DXGI_FORMAT_R16G16B16A16_FLOAT)
	{
		index = 1;

		if (isWindows11_25h2 && g_primaryHdrContext == NULL)
		{
			g_primaryHdrContext = cOverlayContext;
		}
	}

	lutData* lut;
	if (index == -1 || !(lut = GetLUTDataFromCOverlayContext(cOverlayContext, index == 1)))
	{
		char message[256];
		sprintf(message, "LUT not found for index %d", index);
		LOG_ONLY_ONCE(message)
		return false;
	}

	D3D11_TEXTURE2D_DESC oldTextureDesc = textureDesc[index];
	if (newBackBufferDesc.Width > oldTextureDesc.Width || newBackBufferDesc.Height > oldTextureDesc.Height)
	{
		if (texture[index] != NULL)
		{
			texture[index]->Release();
			textureView[index]->Release();
		}

		UINT newWidth = max(newBackBufferDesc.Width, oldTextureDesc.Width);
		UINT newHeight = max(newBackBufferDesc.Height, oldTextureDesc.Height);

		D3D11_TEXTURE2D_DESC newTextureDesc;

		newTextureDesc = newBackBufferDesc;
		newTextureDesc.Width = newWidth;
		newTextureDesc.Height = newHeight;
		newTextureDesc.Usage = D3D11_USAGE_DEFAULT;
		newTextureDesc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
		newTextureDesc.CPUAccessFlags = 0;
		newTextureDesc.MiscFlags = 0;

		textureDesc[index] = newTextureDesc;

		EXECUTE_WITH_LOG(device->CreateTexture2D(&textureDesc[index], NULL, &texture[index]))
		EXECUTE_WITH_LOG(
			device->CreateShaderResourceView((ID3D11Resource*)texture[index], NULL, &textureView[index]))
	}

	backBufferDesc = newBackBufferDesc;
	renderTargetView = NULL;
	for (int i = 0; i < g_RtvCacheCount; i++)
	{
		if (g_RtvCache[i].texture == backBuffer)
		{
			renderTargetView = g_RtvCache[i].rtv;
			renderTargetView->AddRef();
			break;
		}
	}

	if (renderTargetView == NULL)
	{
		if (g_RtvCacheCount >= 16)
		{
			RELEASE_IF_NOT_NULL(g_RtvCache[0].rtv)
			for (int i = 0; i < 15; i++) g_RtvCache[i] = g_RtvCache[i+1];
			g_RtvCacheCount--;
		}
		EXECUTE_WITH_LOG(device->CreateRenderTargetView((ID3D11Resource*)backBuffer, NULL, &g_RtvCache[g_RtvCacheCount].rtv))
		g_RtvCache[g_RtvCacheCount].texture = backBuffer;
		renderTargetView = g_RtvCache[g_RtvCacheCount].rtv;
		renderTargetView->AddRef();
		g_RtvCacheCount++;
	}

	const D3D11_VIEWPORT d3d11_viewport(0, 0, (float)backBufferDesc.Width, (float)backBufferDesc.Height, 0.0f, 1.0f);
	deviceContext->RSSetViewports(1, &d3d11_viewport);

	deviceContext->OMSetRenderTargets(1, &renderTargetView, NULL);
	renderTargetView->Release();

	deviceContext->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
	deviceContext->IASetInputLayout(inputLayout);
	deviceContext->VSSetShader(vertexShader, NULL, 0);
	deviceContext->PSSetShader(pixelShader, NULL, 0);
	deviceContext->PSSetSamplers(0, 1, &samplerState);
	deviceContext->PSSetSamplers(1, 1, &noiseSamplerState);

	deviceContext->PSSetShaderResources(0, 1, &textureView[index]);
	deviceContext->PSSetShaderResources(1, 1, &lut->textureView);
	deviceContext->PSSetShaderResources(2, 1, &noiseTextureView);

	static int lastLutSize[2] = {-1, -1};
	static bool lastIsHdr[2] = {false, false};

	if (lastLutSize[index] != lut->size || lastIsHdr[index] != (index == 1))
	{
		int constantData[4] = {lut->size, index == 1};
		D3D11_MAPPED_SUBRESOURCE resource;
		EXECUTE_WITH_LOG(deviceContext->Map((ID3D11Resource*)constantBuffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &resource))
		memcpy(resource.pData, constantData, sizeof(constantData));
		deviceContext->Unmap((ID3D11Resource*)constantBuffer, 0);
		
		lastLutSize[index] = lut->size;
		lastIsHdr[index] = (index == 1);
	}

	deviceContext->PSSetConstantBuffers(0, 1, &constantBuffer);

	D3D11_BOX sourceBox;
	sourceBox.left = 0;
	sourceBox.top = 0;
	sourceBox.front = 0;
	sourceBox.right = backBufferDesc.Width;
	sourceBox.bottom = backBufferDesc.Height;
	sourceBox.back = 1;
	deviceContext->CopySubresourceRegion((ID3D11Resource*)texture[index], 0, 0, 0, 0, (ID3D11Resource*)backBuffer, 0, &sourceBox);

	for (int i = 0; i < numRects; i++)
	{
		DrawRectangle(&rects[i], index);
	}

	return true;
}

bool ApplyLUT(void* cOverlayContext, IDXGISwapChain* swapChain, struct tagRECT* rects, int numRects)
{
	try
	{
		if (!device)
		{
			LOG_ONLY_ONCE("Initializing stuff in ApplyLUT")
			ID3D11Device* dev;
			EXECUTE_WITH_LOG(swapChain->GetDevice(IID_ID3D11Device, (void**)&dev))
			InitializeStuff(dev);
			dev->Release();
		}
		LOG_ONLY_ONCE("Init done, continuing with LUT application")

		ID3D11Texture2D* backBuffer;
		EXECUTE_WITH_LOG(swapChain->GetBuffer(0, IID_ID3D11Texture2D, (void**)&backBuffer))

		bool result = RenderLUT(cOverlayContext, backBuffer, rects, numRects);
		backBuffer->Release();
		return result;
	}
	catch (std::exception& ex)
	{
		std::stringstream ex_message;
		ex_message << "Exception caught at line " << __LINE__ << ": " << ex.what() << std::endl;
		LOG_ONLY_ONCE(ex_message.str().c_str())
		return false;
	}
	catch (...)
	{
		std::stringstream ex_message;
		ex_message << "Exception caught at line " << __LINE__ << std::endl;
		LOG_ONLY_ONCE(ex_message.str().c_str())
		return false;
	}
}


bool ApplyLUTDirect(void* cOverlayContext, ID3D11Texture2D* backBuffer, struct tagRECT* rects, int numRects)
{
	try
	{
		if (!device)
		{
			LOG_ONLY_ONCE("Initializing from texture device (25H2)")
			ID3D11Device* dev;
			backBuffer->GetDevice(&dev);
			InitializeStuff(dev);
			dev->Release();
		}
		return RenderLUT(cOverlayContext, backBuffer, rects, numRects);
	}
	catch (std::exception& ex)
	{
		std::stringstream ex_message;
		ex_message << "Exception caught at line " << __LINE__ << ": " << ex.what() << std::endl;
		LOG_ONLY_ONCE(ex_message.str().c_str())
		return false;
	}
	catch (...)
	{
		std::stringstream ex_message;
		ex_message << "Exception caught at line " << __LINE__ << std::endl;
		LOG_ONLY_ONCE(ex_message.str().c_str())
		return false;
	}
}

typedef struct rectVec
{
	struct tagRECT* start;
	struct tagRECT* end;
	struct tagRECT* cap;
} rectVec;

static bool SafeReadPointer(void* address, void** value)
{
	__try
	{
		if (address == NULL || value == NULL)
		{
			return false;
		}
		*value = *(void**)address;
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

static bool SafeReadBool(void* address, bool* value)
{
	__try
	{
		if (address == NULL || value == NULL)
		{
			return false;
		}
		*value = *(bool*)address;
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

static bool SafeReadInt(void* address, int* value)
{
	__try
	{
		if (address == NULL || value == NULL)
		{
			return false;
		}
		*value = *(int*)address;
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

static bool TryGetPresentRects(rectVec* dirtyRects, tagRECT** rects, int* rectCount)
{
	__try
	{
		if (dirtyRects == NULL || rects == NULL || rectCount == NULL ||
			dirtyRects->start == NULL || dirtyRects->end == NULL ||
			dirtyRects->end < dirtyRects->start)
		{
			return false;
		}

		ptrdiff_t count = dirtyRects->end - dirtyRects->start;
		if (count <= 0 || count > 4096)
		{
			return false;
		}

		*rects = dirtyRects->start;
		*rectCount = (int)count;
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

typedef long (COverlayContext_Present_t)(void*, void*, unsigned int, rectVec*, unsigned int, bool);
typedef long long (COverlayContext_Present_24h2_t)(void*, void*, unsigned int, rectVec*, int, void*, bool);



static ID3D11Texture2D* GetBackBuffer_25H2(void* overlaySwapChain)
{
	__try
	{
		if (!overlaySwapChain) return NULL;

		void* vtRaw = NULL;
		if (!SafeReadPointer(overlaySwapChain, &vtRaw) || vtRaw == NULL)
		{
			LOG_ONLY_ONCE("25H2 back-buffer lookup failed: overlaySwapChain vtable is unreadable")
			return NULL;
		}
		void** vt = (void**)vtRaw;

		typedef void* (__fastcall *VirtFunc)(void*);

		void* func1Raw = NULL;
		if (!SafeReadPointer(&vt[24], &func1Raw) || func1Raw == NULL)
		{
			LOG_ONLY_ONCE("25H2 back-buffer lookup failed: vt[24] is unreadable")
			return NULL;
		}
		VirtFunc func1 = (VirtFunc)func1Raw;

		void* r1 = func1(overlaySwapChain);
		if (!r1)
		{
			LOG_ONLY_ONCE("25H2 back-buffer lookup failed: vt[24]() returned null")
			return NULL;
		}

		void* vt2Raw = NULL;
		if (!SafeReadPointer(r1, &vt2Raw) || vt2Raw == NULL)
		{
			LOG_ONLY_ONCE("25H2 back-buffer lookup failed: second vtable is unreadable")
			return NULL;
		}
		void** vt2 = (void**)vt2Raw;

		void* func2Raw = NULL;
		if (!SafeReadPointer(&vt2[19], &func2Raw) || func2Raw == NULL)
		{
			LOG_ONLY_ONCE("25H2 back-buffer lookup failed: vt2[19] is unreadable")
			return NULL;
		}
		VirtFunc func2 = (VirtFunc)func2Raw;

		void* r2 = func2(r1);
		if (!r2)
		{
			LOG_ONLY_ONCE("25H2 back-buffer lookup failed: vt2[19]() returned null")
			return NULL;
		}

		ID3D11Texture2D* tex = NULL;
		HRESULT hr = ((IUnknown*)r2)->QueryInterface(IID_ID3D11Texture2D, (void**)&tex);
		if (FAILED(hr) || !tex)
		{
			char message_buf[192];
			sprintf_s(message_buf, "25H2 back-buffer lookup failed: QI(ID3D11Texture2D) hr=0x%08X tex=0x%p", hr, tex);
			LOG_ONLY_ONCE(message_buf)
			return NULL;
		}

		LOG_ONLY_ONCE("25H2: Got texture via overlaySwapChain->vt[24]()->vt2[19]()->QI")
		return tex;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return NULL;
	}
}

COverlayContext_Present_t* COverlayContext_Present_orig = NULL;
COverlayContext_Present_t* COverlayContext_Present_real_orig = NULL;

COverlayContext_Present_24h2_t* COverlayContext_Present_orig_24h2 = NULL;
COverlayContext_Present_24h2_t* COverlayContext_Present_real_orig_24h2 = NULL;

long long COverlayContext_Present_hook_24h2(void* self, void* overlaySwapChain, unsigned int a3, rectVec* rectVec,
	int a5, void* a6, bool a7)
{
	if (_ReturnAddress() < (void*)COverlayContext_Present_real_orig_24h2 || isWindows11_24h2 || isWindows11_25h2)
	{
			LOG_ONLY_ONCE("I am inside COverlayContext::Present hook inside the main if condition")
			tagRECT* presentRects = NULL;
			int presentRectCount = 0;
			if (!TryGetPresentRects(rectVec, &presentRects, &presentRectCount))
			{
				LOG_ONLY_ONCE("Present dirty-rect vector is invalid; skipping LUT for this frame")
				return COverlayContext_Present_orig_24h2(self, overlaySwapChain, a3, rectVec, a5, a6, a7);
			}
			std::stringstream overlay_swapchain_message;
			overlay_swapchain_message << "OverlaySwapChain address: 0x" << std::hex << overlaySwapChain
				<< " -- windows 11 25h2: " << isWindows11_25h2
				<< " -- windows 11 24h2: " << isWindows11_24h2
				<< " -- " << "windows 11: " << isWindows11;
			LOG_ONLY_ONCE(overlay_swapchain_message.str().c_str())

			if (isWindows11_25h2)
			{
				bool success = false;

				ID3D11Texture2D* backBuffer = GetBackBuffer_25H2(overlaySwapChain);
				if (backBuffer)
				{
					if (ApplyLUTDirect(self, backBuffer, presentRects, presentRectCount))
					{
						SetLUTActive(self);
						success = true;
					}
					backBuffer->Release();
				}

				if (!success)
				{
					if (g_DynamicSwapChainOffset != -1)
					{
						void* swapChainRaw = NULL;
						IDXGISwapChain* swapChain = NULL;
						if (SafeReadPointer((unsigned char*)overlaySwapChain + g_DynamicSwapChainOffset, &swapChainRaw))
						{
							swapChain = (IDXGISwapChain*)swapChainRaw;
						}
						if (swapChain != NULL)
						{
							if (ApplyLUT(self, swapChain, presentRects, presentRectCount))
							{
								SetLUTActive(self);
								success = true;
							}
						}
					}

					if (!success)
					{
						for (int off = 0x80; off < 0x240; off += 8)
						{
							void* ptr = NULL;
							if (SafeReadPointer((unsigned char*)overlaySwapChain + off, &ptr) && ptr != NULL)
							{
								void* vtable = NULL;
								SafeReadPointer(ptr, &vtable);
								if (vtable != NULL)
								{
									IDXGISwapChain* swapChain = (IDXGISwapChain*)ptr;

									if (ApplyLUT(self, swapChain, presentRects, presentRectCount))
									{
										g_DynamicSwapChainOffset = off;
										SetLUTActive(self);
										success = true;
										break;
									}
								}
							}
						}
					}
				}

				if (!success)
				{
					UnsetLUTActive(self);
				}
			}
			else
			{

			bool hwProtected = false;
			if (isWindows11_24h2)
				SafeReadBool((bool*)overlaySwapChain + IOverlaySwapChain_HardwareProtected_offset_w11_24h2, &hwProtected);
			else if (isWindows11)
				SafeReadBool((bool*)overlaySwapChain + IOverlaySwapChain_HardwareProtected_offset_w11, &hwProtected);
			else
				SafeReadBool((bool*)overlaySwapChain + IOverlaySwapChain_HardwareProtected_offset, &hwProtected);

			if (hwProtected)
			{
				LOG_ONLY_ONCE("Hardware protected - unsetting LUT active")
				UnsetLUTActive(self);
			}
			else
			{
				IDXGISwapChain* swapChain = NULL;

				if (isWindows11_24h2)
				{
					LOG_ONLY_ONCE("Gathering IDXGISwapChain pointer")
					void* swapChainRaw = NULL;
					if (SafeReadPointer((unsigned char*)overlaySwapChain + IOverlaySwapChain_IDXGISwapChain_offset_w11_24h2, &swapChainRaw))
						swapChain = (IDXGISwapChain*)swapChainRaw;
				}
				else if (isWindows11)
				{
					LOG_ONLY_ONCE("Gathering IDXGISwapChain pointer")
					int sub_from_legacy_swapchain = 0;
					if (SafeReadInt((unsigned char*)overlaySwapChain - 4, &sub_from_legacy_swapchain))
					{
						void* real_overlay_swap_chain = (unsigned char*)overlaySwapChain - sub_from_legacy_swapchain -
							0x1b0;
						void* swapChainRaw = NULL;
						if (SafeReadPointer((unsigned char*)real_overlay_swap_chain + IOverlaySwapChain_IDXGISwapChain_offset_w11, &swapChainRaw))
							swapChain = (IDXGISwapChain*)swapChainRaw;
					}
				}
				else
				{
					void* swapChainRaw = NULL;
					if (SafeReadPointer((unsigned char*)overlaySwapChain + IOverlaySwapChain_IDXGISwapChain_offset, &swapChainRaw))
						swapChain = (IDXGISwapChain*)swapChainRaw;
				}

				if (swapChain != NULL && ApplyLUT(self, swapChain, presentRects, presentRectCount))
				{
					LOG_ONLY_ONCE("Setting LUTactive")
					SetLUTActive(self);
				}
				else
				{
					LOG_ONLY_ONCE("Un-setting LUTactive")
					UnsetLUTActive(self);
				}
			}
			}
	}

	return COverlayContext_Present_orig_24h2(self, overlaySwapChain, a3, rectVec, a5, a6, a7);
}


long COverlayContext_Present_hook(void* self, void* overlaySwapChain, unsigned int a3, rectVec* rectVec,
                                  unsigned int a5, bool a6)
{
	if (_ReturnAddress() < (void*)COverlayContext_Present_real_orig)
	{
		LOG_ONLY_ONCE("I am inside COverlayContext::Present hook inside the main if condition")
		tagRECT* presentRects = NULL;
		int presentRectCount = 0;
		if (!TryGetPresentRects(rectVec, &presentRects, &presentRectCount))
		{
			LOG_ONLY_ONCE("Legacy Present dirty-rect vector is invalid; skipping LUT for this frame")
			return COverlayContext_Present_orig(self, overlaySwapChain, a3, rectVec, a5, a6);
		}

		bool hwProtected = false;
		if (isWindows11)
			SafeReadBool((bool*)overlaySwapChain + IOverlaySwapChain_HardwareProtected_offset_w11, &hwProtected);
		else
			SafeReadBool((bool*)overlaySwapChain + IOverlaySwapChain_HardwareProtected_offset, &hwProtected);

		if (hwProtected)
		{
			LOG_ONLY_ONCE("Hardware protected - unsetting LUT active")
			UnsetLUTActive(self);
		}
		else
		{
			IDXGISwapChain* swapChain = NULL;

			if (isWindows11)
			{
				LOG_ONLY_ONCE("Gathering IDXGISwapChain pointer")
				int sub_from_legacy_swapchain = 0;
				if (SafeReadInt((unsigned char*)overlaySwapChain - 4, &sub_from_legacy_swapchain))
				{
					void* real_overlay_swap_chain = (unsigned char*)overlaySwapChain - sub_from_legacy_swapchain -
						0x1b0;
					void* swapChainRaw = NULL;
					if (SafeReadPointer((unsigned char*)real_overlay_swap_chain + IOverlaySwapChain_IDXGISwapChain_offset_w11, &swapChainRaw))
						swapChain = (IDXGISwapChain*)swapChainRaw;
				}
			}
			else
			{
				void* swapChainRaw = NULL;
				if (SafeReadPointer((unsigned char*)overlaySwapChain + IOverlaySwapChain_IDXGISwapChain_offset, &swapChainRaw))
					swapChain = (IDXGISwapChain*)swapChainRaw;
			}

			if (swapChain != NULL && ApplyLUT(self, swapChain, presentRects, presentRectCount))
			{
				LOG_ONLY_ONCE("Setting LUTactive")
				SetLUTActive(self);
			}
			else
			{
				LOG_ONLY_ONCE("Un-setting LUTactive")
				UnsetLUTActive(self);
			}
		}
	}

	return COverlayContext_Present_orig(self, overlaySwapChain, a3, rectVec, a5, a6);
}

typedef bool (CWindowContext_IsCandidateDirectFlipCompatbile_t)(void*, void*, bool);
CWindowContext_IsCandidateDirectFlipCompatbile_t* CWindowContext_IsCandidateDirectFlipCompatbile_orig = NULL;

bool CWindowContext_IsCandidateDirectFlipCompatbile_hook(void* self, void* a2, bool a3)
{
	if (numLuts > 0)
	{
		return false;
	}
	return CWindowContext_IsCandidateDirectFlipCompatbile_orig(self, a2, a3);
}

typedef bool (CCompSwapChain_IsCandidateDirectFlipCompatbile_t)(void*, void*, bool);
CCompSwapChain_IsCandidateDirectFlipCompatbile_t* CCompSwapChain_IsCandidateDirectFlipCompatbile_orig = NULL;

bool CCompSwapChain_IsCandidateDirectFlipCompatbile_hook(void* self, void* a2, bool a3)
{
	if (numLuts > 0)
	{
		return false;
	}
	return CCompSwapChain_IsCandidateDirectFlipCompatbile_orig(self, a2, a3);
}

typedef bool (CCompVisual_IsCandidateForPromotion_t)(void*, void*, void*);
CCompVisual_IsCandidateForPromotion_t* CCompVisual_IsCandidateForPromotion_orig = NULL;

bool CCompVisual_IsCandidateForPromotion_hook(void* self, void* a2, void* a3)
{
	if (numLuts > 0)
	{
		return false;
	}
	return CCompVisual_IsCandidateForPromotion_orig(self, a2, a3);
}

typedef bool (CCompSwapChain_IsCandidateIndependentFlipCompatible_t)(void*);
CCompSwapChain_IsCandidateIndependentFlipCompatible_t* CCompSwapChain_IsCandidateIndependentFlipCompatible_orig = NULL;

bool CCompSwapChain_IsCandidateIndependentFlipCompatible_hook(void* self)
{
	if (numLuts > 0)
	{
		return false;
	}
	return CCompSwapChain_IsCandidateIndependentFlipCompatible_orig(self);
}

typedef bool (COverlayContext_IsCandidateDirectFlipCompatbile_t)(void*, void*, void*, void*, int, unsigned int, bool,
                                                                 bool);
typedef bool (COverlayContext_IsCandidateDirectFlipCompatbile_24h2_t)(void*, void*, void*, void*, unsigned int, bool);

COverlayContext_IsCandidateDirectFlipCompatbile_t* COverlayContext_IsCandidateDirectFlipCompatbile_orig;
COverlayContext_IsCandidateDirectFlipCompatbile_24h2_t* COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2;

bool COverlayContext_IsCandidateDirectFlipCompatbile_hook_24h2(void* self, void* a2, void* a3, void* a4, unsigned int a5,
	bool a6)
{
	if (IsLUTActive(self))
	{
		return false;
	}
	return COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2(self, a2, a3, a4, a5, a6);
}

bool COverlayContext_IsCandidateDirectFlipCompatbile_hook(void* self, void* a2, void* a3, void* a4, int a5,
                                                          unsigned int a6, bool a7, bool a8)
{
	if (IsLUTActive(self))
	{
		return false;
	}
	return COverlayContext_IsCandidateDirectFlipCompatbile_orig(self, a2, a3, a4, a5, a6, a7, a8);
}

typedef bool (COverlayContext_OverlaysEnabled_t)(void*);

COverlayContext_OverlaysEnabled_t* COverlayContext_OverlaysEnabled_orig  = NULL;

bool COverlayContext_OverlaysEnabled_hook(void* self)
{
	if (IsLUTActive(self))
	{
		return false;
	}
	return COverlayContext_OverlaysEnabled_orig(self);
}

DwmProfileMachine GetCompiledProfileMachine()
{
#if defined(_M_ARM64)
	return DwmProfileMachineArm64;
#elif defined(_M_X64) || defined(_M_AMD64)
	return DwmProfileMachineX64;
#else
	return (DwmProfileMachine)0;
#endif
}

const char* GetMachineName(uint32_t machine)
{
	switch (machine)
	{
	case DwmProfileMachineX64:
		return "x64";
	case DwmProfileMachineArm64:
		return "arm64";
	default:
		return "unknown";
	}
}

bool TryReadPeMachine(HMODULE module, WORD* machine)
{
	__try
	{
		BYTE* base = (BYTE*)module;
		IMAGE_DOS_HEADER* dos = (IMAGE_DOS_HEADER*)base;
		if (dos->e_magic != IMAGE_DOS_SIGNATURE)
		{
			return false;
		}

		IMAGE_NT_HEADERS* nt = (IMAGE_NT_HEADERS*)(base + dos->e_lfanew);
		if (nt->Signature != IMAGE_NT_SIGNATURE)
		{
			return false;
		}

		*machine = nt->FileHeader.Machine;
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool TryReadPdbIdentity(HMODULE module, char* guidBuf, size_t guidBufLen, DWORD* age)
{
	__try
	{
		BYTE* base = (BYTE*)module;
		IMAGE_DOS_HEADER* dos = (IMAGE_DOS_HEADER*)base;
		if (dos->e_magic != IMAGE_DOS_SIGNATURE)
		{
			return false;
		}

		IMAGE_NT_HEADERS* nt = (IMAGE_NT_HEADERS*)(base + dos->e_lfanew);
		if (nt->Signature != IMAGE_NT_SIGNATURE)
		{
			return false;
		}

		IMAGE_DATA_DIRECTORY debugDir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_DEBUG];
		if (debugDir.VirtualAddress == 0 || debugDir.Size < sizeof(IMAGE_DEBUG_DIRECTORY))
		{
			return false;
		}

		IMAGE_DEBUG_DIRECTORY* entries = (IMAGE_DEBUG_DIRECTORY*)(base + debugDir.VirtualAddress);
		DWORD count = debugDir.Size / sizeof(IMAGE_DEBUG_DIRECTORY);
		for (DWORD i = 0; i < count; i++)
		{
			if (entries[i].Type != IMAGE_DEBUG_TYPE_CODEVIEW || entries[i].AddressOfRawData == 0)
			{
				continue;
			}

			BYTE* cv = base + entries[i].AddressOfRawData;
			if (*(DWORD*)cv != 0x53445352) // RSDS
			{
				continue;
			}

			GUID* guid = (GUID*)(cv + 4);
			*age = *(DWORD*)(cv + 20);
			sprintf_s(guidBuf, guidBufLen,
				"%08lX-%04X-%04X-%02X%02X-%02X%02X%02X%02X%02X%02X",
				(unsigned long)guid->Data1,
				guid->Data2,
				guid->Data3,
				guid->Data4[0],
				guid->Data4[1],
				guid->Data4[2],
				guid->Data4[3],
				guid->Data4[4],
				guid->Data4[5],
				guid->Data4[6],
				guid->Data4[7]);
			return true;
		}
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}

	return false;
}

bool ApplyKnownDwmProfile(HMODULE dwmcore)
{
	WORD dwmMachine = 0;
	DwmProfileMachine compiledMachine = GetCompiledProfileMachine();
	if (TryReadPeMachine(dwmcore, &dwmMachine))
	{
		std::stringstream machineLog;
		machineLog << "Architecture check: dwmcore=" << GetMachineName(dwmMachine)
			<< " (0x" << std::hex << dwmMachine << "), dll=" << GetMachineName(compiledMachine)
			<< " (0x" << std::hex << compiledMachine << ")";
		LOG_ONLY_ONCE(machineLog.str().c_str())

		if (compiledMachine == 0 || dwmMachine != compiledMachine)
		{
			LOG_ONLY_ONCE("DWM architecture does not match this dwm_lut.dll build; refusing exact-profile hooks")
			return false;
		}
	}

	char pdbGuid[64] = {};
	DWORD pdbAge = 0;
	if (!TryReadPdbIdentity(dwmcore, pdbGuid, sizeof(pdbGuid), &pdbAge))
	{
		LOG_ONLY_ONCE("No CodeView PDB identity found for dwmcore.dll")
		return false;
	}

	std::stringstream identity;
	identity << "dwmcore PDB identity: " << pdbGuid << ":" << pdbAge;
	LOG_ONLY_ONCE(identity.str().c_str())

	for (int i = 0; i < (int)(sizeof(kDwmSymbolProfiles) / sizeof(kDwmSymbolProfiles[0])); i++)
	{
		const DwmSymbolProfile* profile = &kDwmSymbolProfiles[i];
		if (_stricmp(profile->pdbGuid, pdbGuid) != 0 || profile->pdbAge != pdbAge)
		{
			continue;
		}
		if (profile->machine != compiledMachine)
		{
			std::stringstream skipped;
			skipped << "Skipping DWM symbol profile " << profile->id << " because profile architecture "
				<< GetMachineName(profile->machine) << " does not match dll architecture "
				<< GetMachineName(compiledMachine);
			LOG_ONLY_ONCE(skipped.str().c_str())
			continue;
		}

		g_activeDwmProfile = profile;
		std::stringstream selected;
		selected << "Selected DWM symbol profile: " << profile->id
			<< " arch=" << GetMachineName(profile->machine)
			<< " engine=" << profile->engineFamily
			<< " presentAbi=" << profile->presentAbi
			<< " swapchain=" << profile->swapchainStrategy
			<< " backbuffer=" << profile->backbufferStrategy
			<< " validation=" << profile->validationState;
		LOG_ONLY_ONCE(selected.str().c_str())

		if (_stricmp(profile->presentAbi, "overlay-present-modern7") == 0)
		{
			isWindows11 = true;
			isWindows11_24h2 = false;
			isWindows11_25h2 = true;
		}

		if (profile->presentRva != 0)
		{
			COverlayContext_Present_orig_24h2 = (COverlayContext_Present_24h2_t*)((BYTE*)dwmcore + profile->presentRva);
			COverlayContext_Present_real_orig_24h2 = COverlayContext_Present_orig_24h2;
			LOG_ADDRESS("Profile COverlayContext::Present", COverlayContext_Present_orig_24h2)
		}
		if (profile->directFlipCompatibleRva != 0)
		{
			COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 = (
				COverlayContext_IsCandidateDirectFlipCompatbile_24h2_t*)((BYTE*)dwmcore + profile->directFlipCompatibleRva);
			LOG_ADDRESS("Profile COverlayContext::IsCandidateDirectFlipCompatible", COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2)
		}
		if (profile->overlaysEnabledRva != 0)
		{
			COverlayContext_OverlaysEnabled_orig = (COverlayContext_OverlaysEnabled_t*)((BYTE*)dwmcore + profile->overlaysEnabledRva);
			LOG_ADDRESS("Profile COverlayContext::OverlaysEnabled", COverlayContext_OverlaysEnabled_orig)
		}
		if (profile->overlayTestModeRva != 0)
		{
			g_pOverlayTestMode = (int*)((BYTE*)dwmcore + profile->overlayTestModeRva);
			LOG_ADDRESS("Profile CCommonRegistryData::m_dwOverlayTestMode", g_pOverlayTestMode)
		}

		return COverlayContext_Present_orig_24h2 != NULL;
	}

	LOG_ONLY_ONCE("No matching DWM symbol profile; falling back to byte signatures")
	return false;
}

void ForceOverlayTestMode()
{
	if (g_pOverlayTestMode == NULL)
	{
		LOG_ONLY_ONCE("FAILED to find g_pOverlayTestMode")
		return;
	}

	__try
	{
		if (!g_hasPreviousOverlayTestMode)
		{
			g_previousOverlayTestMode = *g_pOverlayTestMode;
			g_hasPreviousOverlayTestMode = true;
		}
		*g_pOverlayTestMode = 5;
		LOG_ONLY_ONCE("Forced OverlayTestMode to 5")
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		LOG_ONLY_ONCE("Exception while writing OverlayTestMode")
	}
}

bool LogMinHookStatus(MH_STATUS status, const char* operation)
{
	if (status == MH_OK)
	{
		return true;
	}

	std::stringstream ss;
	ss << operation << " failed: " << MH_StatusToString(status) << " (" << status << ")";
	log_to_file(ss.str().c_str());
	return false;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD fdwReason, LPVOID lpReserved)
{
	switch (fdwReason)
	{
	case DLL_PROCESS_ATTACH:
		{
			HMODULE dwmcore = GetModuleHandle(L"dwmcore.dll");
			if (dwmcore == NULL)
			{
				log_to_file("dwmcore.dll is not loaded; cannot initialize DWM LUT");
				return FALSE;
			}
			MODULEINFO moduleInfo = {};
			if (!GetModuleInformation(GetCurrentProcess(), dwmcore, &moduleInfo, sizeof moduleInfo))
			{
				print_error("GetModuleInformation(dwmcore.dll) failed");
				return FALSE;
			}

			OSVERSIONINFOEX versionInfo;
			ZeroMemory(&versionInfo, sizeof OSVERSIONINFOEX);
			versionInfo.dwOSVersionInfoSize = sizeof OSVERSIONINFOEX;
			versionInfo.dwBuildNumber = 22000;


			OSVERSIONINFOEX versionInfo24h2;
			ZeroMemory(&versionInfo24h2, sizeof OSVERSIONINFOEX);
			versionInfo24h2.dwOSVersionInfoSize = sizeof OSVERSIONINFOEX;
			versionInfo24h2.dwBuildNumber = 26100;


			OSVERSIONINFOEX versionInfo25h2;
			ZeroMemory(&versionInfo25h2, sizeof OSVERSIONINFOEX);
			versionInfo25h2.dwOSVersionInfoSize = sizeof OSVERSIONINFOEX;
			versionInfo25h2.dwBuildNumber = 26200;


			ULONGLONG dwlConditionMask = 0;
			VER_SET_CONDITION(dwlConditionMask, VER_BUILDNUMBER, VER_GREATER_EQUAL);

			if (VerifyVersionInfo(&versionInfo25h2, VER_BUILDNUMBER, dwlConditionMask))
			{
				isWindows11_25h2 = true;
			}
			else if (VerifyVersionInfo(&versionInfo24h2, VER_BUILDNUMBER, dwlConditionMask))
			{
				isWindows11_24h2 = true;
			}
			else if (VerifyVersionInfo(&versionInfo, VER_BUILDNUMBER, dwlConditionMask))
			{
				isWindows11 = true;
			}
			else
			{
				isWindows11 = false;
			}

			char lutFolderPath[MAX_PATH];
			ExpandEnvironmentStringsA(LUT_FOLDER, lutFolderPath, sizeof(lutFolderPath));
			ReadResolverConfig(lutFolderPath);
			ApplyResolverModeToVersionFlags();

			bool usedKnownDwmProfile = false;
			if (ResolverAllowsExactProfile())
			{
				usedKnownDwmProfile = ApplyKnownDwmProfile(dwmcore);
			}
			if (g_resolverMode == ResolverExactProfileOnly && !usedKnownDwmProfile)
			{
				log_to_file("Resolver exact-profile-only requested, but no exact profile matched this dwmcore.dll");
				return FALSE;
			}

			if (!usedKnownDwmProfile)
			{
				if (isWindows11_25h2)
				{
					if (moduleInfo.SizeOfImage < sizeof COverlayContext_OverlaysEnabled_bytes_w11_25h2)
					{
						log_to_file("dwmcore.dll image is too small for 25H2 signature scan");
					}
					else for (size_t i = 0; i <= moduleInfo.SizeOfImage - sizeof COverlayContext_OverlaysEnabled_bytes_w11_25h2; i++)
					{
						unsigned char* address = (unsigned char*)dwmcore + i;
						if (!COverlayContext_Present_orig_24h2 && sizeof COverlayContext_Present_bytes_w11_25h2 <= moduleInfo.
							SizeOfImage - i && !aob_match_inverse(address, COverlayContext_Present_bytes_w11_25h2,
								sizeof COverlayContext_Present_bytes_w11_25h2))
						{
							COverlayContext_Present_orig_24h2 = (COverlayContext_Present_24h2_t*)address;
							COverlayContext_Present_real_orig_24h2 = COverlayContext_Present_orig_24h2;
						}
						else if (!COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 && sizeof
							COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_25h2 <= moduleInfo.SizeOfImage - i && !
							aob_match_inverse(
								address, COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_25h2,
								sizeof COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_25h2))
						{
							COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 = (
								COverlayContext_IsCandidateDirectFlipCompatbile_24h2_t*)address;
						}
						else if (!CWindowContext_IsCandidateDirectFlipCompatbile_orig && sizeof CWindowContext_IsCandidateDirectFlipCompatible_bytes_w11_25h2
							<= moduleInfo.SizeOfImage - i && !aob_match_inverse(
								address, CWindowContext_IsCandidateDirectFlipCompatible_bytes_w11_25h2,
								sizeof CWindowContext_IsCandidateDirectFlipCompatible_bytes_w11_25h2))
						{
							CWindowContext_IsCandidateDirectFlipCompatbile_orig = (CWindowContext_IsCandidateDirectFlipCompatbile_t*)address;
						}
						else if (!CCompSwapChain_IsCandidateDirectFlipCompatbile_orig && sizeof CCompSwapChain_IsCandidateDirectFlipCompatible_bytes_w11_25h2
							<= moduleInfo.SizeOfImage - i && !aob_match_inverse(
								address, CCompSwapChain_IsCandidateDirectFlipCompatible_bytes_w11_25h2,
								sizeof CCompSwapChain_IsCandidateDirectFlipCompatible_bytes_w11_25h2))
						{
							CCompSwapChain_IsCandidateDirectFlipCompatbile_orig = (CCompSwapChain_IsCandidateDirectFlipCompatbile_t*)address;
						}
						else if (!CCompVisual_IsCandidateForPromotion_orig && sizeof CCompVisual_IsCandidateForPromotion_bytes_w11_25h2
							<= moduleInfo.SizeOfImage - i && !aob_match_inverse(
								address, CCompVisual_IsCandidateForPromotion_bytes_w11_25h2,
								sizeof CCompVisual_IsCandidateForPromotion_bytes_w11_25h2))
						{
							CCompVisual_IsCandidateForPromotion_orig = (CCompVisual_IsCandidateForPromotion_t*)address;
						}
						else if (!COverlayContext_OverlaysEnabled_orig && sizeof COverlayContext_OverlaysEnabled_bytes_w11_25h2
							<= moduleInfo.SizeOfImage - i && !aob_match_inverse(
								address, COverlayContext_OverlaysEnabled_bytes_w11_25h2,
								sizeof COverlayContext_OverlaysEnabled_bytes_w11_25h2))
						{
							COverlayContext_OverlaysEnabled_orig = (COverlayContext_OverlaysEnabled_t*)address;

							int rip_offset = *(int*)(address + 2);
							g_pOverlayTestMode = (int*)(address + 7 + rip_offset);

							const unsigned char flipMatch[] = { 0x48, 0x8D, 0x05 };
							for (int j = 0; j < 500; j++) {
								unsigned char* fAddr = address + j;
								if (!memcmp(fAddr, flipMatch, 3)) {
									CCompSwapChain_IsCandidateIndependentFlipCompatible_orig = (CCompSwapChain_IsCandidateIndependentFlipCompatible_t*)fAddr;
									break;
								}
							}
						}
						if (COverlayContext_Present_orig_24h2 && COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 &&
							COverlayContext_OverlaysEnabled_orig)
						{
							break;
						}
					}
				}
				else if (isWindows11_24h2)
			{
				if (moduleInfo.SizeOfImage < sizeof COverlayContext_OverlaysEnabled_bytes_relative_w11_24h2)
				{
					log_to_file("dwmcore.dll image is too small for 24H2 signature scan");
				}
				else for (size_t i = 0; i <= moduleInfo.SizeOfImage - sizeof COverlayContext_OverlaysEnabled_bytes_relative_w11_24h2; i++)
				{
					unsigned char* address = (unsigned char*)dwmcore + i;
					if (!COverlayContext_Present_orig_24h2 && sizeof COverlayContext_Present_bytes_w11_24h2 <= moduleInfo.
						SizeOfImage - i && !aob_match_inverse(address, COverlayContext_Present_bytes_w11_24h2,
							sizeof COverlayContext_Present_bytes_w11_24h2))
					{
						COverlayContext_Present_orig_24h2 = (COverlayContext_Present_24h2_t*)address;
						COverlayContext_Present_real_orig_24h2 = COverlayContext_Present_orig_24h2;
					}
					else if (!COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 && sizeof
						COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_24h2 <= moduleInfo.SizeOfImage - i && !
						aob_match_inverse(
							address, COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_24h2,
							sizeof COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_24h2))
					{
						COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 = (
							COverlayContext_IsCandidateDirectFlipCompatbile_24h2_t*)address;
					}
					else if (!COverlayContext_OverlaysEnabled_orig && sizeof COverlayContext_OverlaysEnabled_bytes_relative_w11_24h2
						<= moduleInfo.SizeOfImage - i && !aob_match_inverse(
							address, COverlayContext_OverlaysEnabled_bytes_relative_w11_24h2,
							sizeof COverlayContext_OverlaysEnabled_bytes_relative_w11_24h2))
					{


						COverlayContext_OverlaysEnabled_orig = (COverlayContext_OverlaysEnabled_t*)get_relative_address(address, 1, 5);
					}
					if (COverlayContext_Present_orig_24h2 && COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 &&
						COverlayContext_OverlaysEnabled_orig)
					{
						break;
					}
				}
			}
			else if (isWindows11)
			{
				if (moduleInfo.SizeOfImage < sizeof COverlayContext_OverlaysEnabled_bytes_w11)
				{
					log_to_file("dwmcore.dll image is too small for Windows 11 signature scan");
				}
				else for (size_t i = 0; i <= moduleInfo.SizeOfImage - sizeof COverlayContext_OverlaysEnabled_bytes_w11; i++)
				{
					unsigned char* address = (unsigned char*)dwmcore + i;
					if (!COverlayContext_Present_orig && sizeof COverlayContext_Present_bytes_w11 <= moduleInfo.
						SizeOfImage - i && !aob_match_inverse(address, COverlayContext_Present_bytes_w11,
						                                      sizeof COverlayContext_Present_bytes_w11))
					{
						COverlayContext_Present_orig = (COverlayContext_Present_t*)address;
						COverlayContext_Present_real_orig = COverlayContext_Present_orig;
					}
					else if (!COverlayContext_IsCandidateDirectFlipCompatbile_orig && sizeof
						COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11 <= moduleInfo.SizeOfImage - i && !
						aob_match_inverse(
							address, COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11,
							sizeof COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11))
					{
						COverlayContext_IsCandidateDirectFlipCompatbile_orig = (
							COverlayContext_IsCandidateDirectFlipCompatbile_t*)address;
					}
					else if (!COverlayContext_OverlaysEnabled_orig && sizeof COverlayContext_OverlaysEnabled_bytes_w11
						<= moduleInfo.SizeOfImage - i && !aob_match_inverse(
							address, COverlayContext_OverlaysEnabled_bytes_w11,
							sizeof COverlayContext_OverlaysEnabled_bytes_w11))
					{
						COverlayContext_OverlaysEnabled_orig = (COverlayContext_OverlaysEnabled_t*)address;
					}
					if (COverlayContext_Present_orig && COverlayContext_IsCandidateDirectFlipCompatbile_orig &&
						COverlayContext_OverlaysEnabled_orig)
					{
						break;
					}
				}

				DWORD rev;
				DWORD revSize = sizeof(rev);
				RegGetValueA(HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "UBR", RRF_RT_DWORD,
				             NULL, &rev, &revSize);

				if (rev >= 706)
				{

				}
			}
			else
			{
				if (moduleInfo.SizeOfImage < sizeof(COverlayContext_Present_bytes))
				{
					log_to_file("dwmcore.dll image is too small for legacy signature scan");
				}
				else for (size_t i = 0; i <= moduleInfo.SizeOfImage - sizeof(COverlayContext_Present_bytes); i++)
				{
					unsigned char* address = (unsigned char*)dwmcore + i;
					if (!COverlayContext_Present_orig && !memcmp(address, COverlayContext_Present_bytes,
					                                             sizeof(COverlayContext_Present_bytes)))
					{
						COverlayContext_Present_orig = (COverlayContext_Present_t*)address;
						COverlayContext_Present_real_orig = COverlayContext_Present_orig;
					}
					else if (!COverlayContext_IsCandidateDirectFlipCompatbile_orig && !memcmp(
						address, COverlayContext_IsCandidateDirectFlipCompatbile_bytes,
						sizeof(COverlayContext_IsCandidateDirectFlipCompatbile_bytes)))
					{
						static int found = 0;
						found++;
						if (found == 2)
						{
							COverlayContext_IsCandidateDirectFlipCompatbile_orig = (
								COverlayContext_IsCandidateDirectFlipCompatbile_t*)(address - 0xa);
						}
					}
					else if (!COverlayContext_OverlaysEnabled_orig && !memcmp(
						address, COverlayContext_OverlaysEnabled_bytes, sizeof(COverlayContext_OverlaysEnabled_bytes)))
					{
						COverlayContext_OverlaysEnabled_orig = (COverlayContext_OverlaysEnabled_t*)(address - 0x7);
					}
					if (COverlayContext_Present_orig && COverlayContext_IsCandidateDirectFlipCompatbile_orig &&
						COverlayContext_OverlaysEnabled_orig)
					{
						break;
					}
				}
			}
			}

			if (!AddLUTs(lutFolderPath))
			{
				return FALSE;
			}
			bool hasLegacyHookSet = COverlayContext_Present_orig && COverlayContext_IsCandidateDirectFlipCompatbile_orig &&
				COverlayContext_OverlaysEnabled_orig;
			bool hasModernHookSet = COverlayContext_Present_orig_24h2 &&
				(COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 || COverlayContext_OverlaysEnabled_orig || g_pOverlayTestMode != NULL);
			if ((hasLegacyHookSet || hasModernHookSet) && numLuts != 0)

			{
				if (!LogMinHookStatus(MH_Initialize(), "MH_Initialize"))
				{
					return FALSE;
				}
				if (!isWindows11_24h2 && !isWindows11_25h2)
				{
					if (!LogMinHookStatus(MH_CreateHook((PVOID)COverlayContext_Present_orig, (PVOID)COverlayContext_Present_hook,
								  (PVOID*)&COverlayContext_Present_orig), "MH_CreateHook(COverlayContext::Present legacy)"))
					{
						MH_Uninitialize();
						return FALSE;
					}
				}
				else
				{
					if (!LogMinHookStatus(MH_CreateHook((PVOID)COverlayContext_Present_orig_24h2, (PVOID)COverlayContext_Present_hook_24h2,
						(PVOID*)&COverlayContext_Present_orig_24h2), "MH_CreateHook(COverlayContext::Present modern)"))
					{
						MH_Uninitialize();
						return FALSE;
					}
				}

				if (!isWindows11_24h2 && !isWindows11_25h2 && COverlayContext_IsCandidateDirectFlipCompatbile_orig)
					LogMinHookStatus(MH_CreateHook((PVOID)COverlayContext_IsCandidateDirectFlipCompatbile_orig,
								  (PVOID)COverlayContext_IsCandidateDirectFlipCompatbile_hook,
								  (PVOID*)&COverlayContext_IsCandidateDirectFlipCompatbile_orig), "MH_CreateHook(COverlayContext::IsCandidateDirectFlipCompatible legacy)");
				else if (COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2)
					LogMinHookStatus(MH_CreateHook((PVOID)COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2,
						(PVOID)COverlayContext_IsCandidateDirectFlipCompatbile_hook_24h2,
						(PVOID*)&COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2), "MH_CreateHook(COverlayContext::IsCandidateDirectFlipCompatible modern)");
				else
					LOG_ONLY_ONCE("DirectFlip compatibility hook unavailable for this profile")

				if (CWindowContext_IsCandidateDirectFlipCompatbile_orig)
				{
					LogMinHookStatus(MH_CreateHook((PVOID)CWindowContext_IsCandidateDirectFlipCompatbile_orig,
						(PVOID)CWindowContext_IsCandidateDirectFlipCompatbile_hook,
						(PVOID*)&CWindowContext_IsCandidateDirectFlipCompatbile_orig), "MH_CreateHook(CWindowContext::IsCandidateDirectFlipCompatible)");
					LOG_ONLY_ONCE("Hooked CWindowContext::IsCandidateDirectFlipCompatible")
				}
				else {
					LOG_ONLY_ONCE("FAILED to find CWindowContext::IsCandidateDirectFlipCompatible")
				}

				if (CCompSwapChain_IsCandidateIndependentFlipCompatible_orig)
				{
					LogMinHookStatus(MH_CreateHook((PVOID)CCompSwapChain_IsCandidateIndependentFlipCompatible_orig,
						(PVOID)CCompSwapChain_IsCandidateIndependentFlipCompatible_hook,
						(PVOID*)&CCompSwapChain_IsCandidateIndependentFlipCompatible_orig), "MH_CreateHook(CCompSwapChain::IsCandidateIndependentFlipCompatible)");
					LOG_ONLY_ONCE("Hooked CCompSwapChain::IsCandidateIndependentFlipCompatible")
				}
				else {
					LOG_ONLY_ONCE("FAILED to find CCompSwapChain::IsCandidateIndependentFlipCompatible")
				}

				if (CCompSwapChain_IsCandidateDirectFlipCompatbile_orig)
				{
					LogMinHookStatus(MH_CreateHook((PVOID)CCompSwapChain_IsCandidateDirectFlipCompatbile_orig,
						(PVOID)CCompSwapChain_IsCandidateDirectFlipCompatbile_hook,
						(PVOID*)&CCompSwapChain_IsCandidateDirectFlipCompatbile_orig), "MH_CreateHook(CCompSwapChain::IsCandidateDirectFlipCompatible)");
					LOG_ONLY_ONCE("Hooked CCompSwapChain::IsCandidateDirectFlipCompatible")
				}
				else {
					LOG_ONLY_ONCE("FAILED to find CCompSwapChain::IsCandidateDirectFlipCompatible")
				}

				if (CCompVisual_IsCandidateForPromotion_orig)
				{
					LogMinHookStatus(MH_CreateHook((PVOID)CCompVisual_IsCandidateForPromotion_orig,
						(PVOID)CCompVisual_IsCandidateForPromotion_hook,
						(PVOID*)&CCompVisual_IsCandidateForPromotion_orig), "MH_CreateHook(CCompVisual::IsCandidateForPromotion)");
					LOG_ONLY_ONCE("Hooked CCompVisual::IsCandidateForPromotion")
				}
				else {
					LOG_ONLY_ONCE("FAILED to find CCompVisual::IsCandidateForPromotion")
				}

				ForceOverlayTestMode();

				if (COverlayContext_OverlaysEnabled_orig)
				{
					LogMinHookStatus(MH_CreateHook((PVOID)COverlayContext_OverlaysEnabled_orig, (PVOID)COverlayContext_OverlaysEnabled_hook,
						(PVOID*)&COverlayContext_OverlaysEnabled_orig), "MH_CreateHook(COverlayContext::OverlaysEnabled)");
					LOG_ONLY_ONCE("Hooked COverlayContext::OverlaysEnabled")
				}
				else
				{
					LOG_ONLY_ONCE("COverlayContext::OverlaysEnabled hook unavailable for this profile")
				}
				if (!LogMinHookStatus(MH_EnableHook(MH_ALL_HOOKS), "MH_EnableHook(MH_ALL_HOOKS)"))
				{
					MH_Uninitialize();
					return FALSE;
				}
				LOG_ONLY_ONCE("DWM HOOK DLL INITIALIZATION. START LOGGING")

				ForceOverlayTestMode();

				break;
			}
			return FALSE;
		}
	case DLL_PROCESS_DETACH:

		if (g_pOverlayTestMode != NULL)
		{
			__try
			{
				*g_pOverlayTestMode = g_hasPreviousOverlayTestMode ? g_previousOverlayTestMode : 0;
			}
			__except (EXCEPTION_EXECUTE_HANDLER)
			{
				LOG_ONLY_ONCE("Exception while restoring OverlayTestMode")
			}
		}
		MH_Uninitialize();
		UninitializeStuff();
		break;
	default:
		break;
	}
	return TRUE;
}

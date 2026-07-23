import { GlobalConfigApi } from '@/api/facebook/globalconfig'

const CACHE_KEY = 'facebook_global_browser_config'
const DEFAULT_CONFIG = {
  disableImages: false,
  disableVideos: true,
  maxConcurrent: 19
}

export interface FacebookGlobalBrowserConfig {
  disableImages: boolean
  disableVideos: boolean
  maxConcurrent: number
  updatedAt?: number
}

function normalizeConfig(values: Record<string, unknown>): FacebookGlobalBrowserConfig {
  const maxConcurrent = Number.parseInt(String(values.browser_max_concurrent ?? ''), 10)
  return {
    disableImages: values.browser_disable_images === true || values.browser_disable_images === 'true',
    disableVideos: values.browser_disable_videos === undefined
      ? DEFAULT_CONFIG.disableVideos
      : values.browser_disable_videos === true || values.browser_disable_videos === 'true',
    maxConcurrent: Math.min(Math.max(Number.isFinite(maxConcurrent) ? maxConcurrent : DEFAULT_CONFIG.maxConcurrent, 1), 50),
    updatedAt: Date.now()
  }
}

function readCachedConfig(): FacebookGlobalBrowserConfig | null {
  try {
    const raw = window.localStorage.getItem(CACHE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<FacebookGlobalBrowserConfig>
    if (typeof parsed.disableImages !== 'boolean' || typeof parsed.disableVideos !== 'boolean') return null
    if (!Number.isFinite(parsed.maxConcurrent)) return null
    return normalizeConfig({
      browser_disable_images: parsed.disableImages,
      browser_disable_videos: parsed.disableVideos,
      browser_max_concurrent: parsed.maxConcurrent
    })
  } catch {
    return null
  }
}

function writeCachedConfig(config: FacebookGlobalBrowserConfig): void {
  try {
    window.localStorage.setItem(CACHE_KEY, JSON.stringify(config))
  } catch {
    // A restricted WebView storage must not prevent WPF synchronization.
  }
}

async function syncToWpf(config: FacebookGlobalBrowserConfig): Promise<boolean> {
  for (let attempt = 0; attempt < 10; attempt++) {
    const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
    if (bridge?.UpdateGlobalConfig) {
      try {
        await Promise.resolve(bridge.UpdateGlobalConfig(
          config.disableImages,
          config.disableVideos,
          config.maxConcurrent
        ))
        return true
      } catch (error) {
        console.warn('[WPF] 更新 Facebook 浏览器配置失败，将重试:', error)
      }
    }
    await new Promise((resolve) => window.setTimeout(resolve, 300))
  }
  console.warn('[WPF] 桥接对象未就绪，浏览器配置暂未同步')
  return false
}

export async function syncFacebookGlobalConfig(values: Record<string, unknown>): Promise<FacebookGlobalBrowserConfig> {
  const config = normalizeConfig(values)
  writeCachedConfig(config)
  await syncToWpf(config)
  return config
}

export async function syncCachedFacebookGlobalConfigToWpf(): Promise<boolean> {
  const cached = readCachedConfig()
  if (!cached) return false
  return syncToWpf(cached)
}

export async function loadAndSyncFacebookGlobalConfig(): Promise<FacebookGlobalBrowserConfig | null> {
  // Apply the last known value immediately, then replace it with the server value.
  await syncCachedFacebookGlobalConfigToWpf()
  try {
    const configs = await GlobalConfigApi.getAllConfigs()
    const values = Object.fromEntries((configs || []).map((item: any) => [item.configKey, item.configValue]))
    return syncFacebookGlobalConfig(values)
  } catch (error) {
    console.warn('[WPF] 读取 Facebook 全局配置失败，继续使用缓存:', error)
    return readCachedConfig()
  }
}

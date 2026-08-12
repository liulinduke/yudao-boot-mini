import { FbWarmupApi } from '@/api/facebook/warmup'
import { getFbAccountProxyJson } from '@/utils/fbAccountProxy'
import { startBrowserCollect } from '@/utils/wpfBridge'

const startedDetails = new Set<string>()

export function isWarmupDetail(detailId: string | number | undefined) {
  return Boolean(detailId && startedDetails.has(String(detailId)))
}

export async function claimAndStartPendingWarmupTasks() {
  const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
  if (!bridge?.GetAvailableBrowserSlots || !bridge.StartBrowser) return
  const slots = Math.max(Number(bridge.GetAvailableBrowserSlots() || 0), 0)
  if (slots <= 0) return
  const items: any[] = await FbWarmupApi.claimPending(Math.min(slots, 10))
  for (const item of items || []) {
    const detailId = String(item.detailId)
    if (startedDetails.has(detailId)) continue
    startedDetails.add(detailId)
    try {
      const accountId = String(item.fbAccount || item.accountId)
      const proxyConfigJson = await getFbAccountProxyJson(accountId)
      await startBrowserCollect(
        detailId,
        accountId,
        item.cookie || null,
        'https://www.facebook.com',
        0,
        17,
        item.warmupConfig,
        true,
        item.deviceId || undefined,
        item.password || undefined,
        item.tfa || undefined,
        item.fbAccount || undefined,
        proxyConfigJson
      )
    } catch (error) {
      startedDetails.delete(detailId)
      await FbWarmupApi.reportDetail(detailId, false, error instanceof Error ? error.message : '启动养号失败')
    }
  }
}

export async function reportWarmupDetail(detailId: string | number, ok: boolean, errorMessage?: string) {
  const key = String(detailId)
  if (!startedDetails.has(key)) return
  startedDetails.delete(key)
  await FbWarmupApi.reportDetail(key, ok, errorMessage)
}

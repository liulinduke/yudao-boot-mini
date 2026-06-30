import { FbAiAgentApi, type FbAiAgentDispatchDetail } from '@/api/facebook/aiagent'
import { startBrowserCollect } from '@/utils/wpfBridge'

let timer: number | undefined
let polling = false
const claimedDetailIds = new Set<string>()

const getBridge = () => window.chrome?.webview?.hostObjects?.sync?.wpfBridge

const getAvailableSlots = () => {
  const bridge = getBridge()
  if (!bridge) return 0
  try {
    if (typeof bridge.GetAvailableBrowserSlots === 'function') {
      return Math.max(Number(bridge.GetAvailableBrowserSlots()) || 0, 0)
    }
  } catch (error) {
    console.warn('获取WPF浏览器空闲槽位失败', error)
  }
  return 1
}

export const startAiAgentCollectDetail = (detail: FbAiAgentDispatchDetail) => {
  if (!detail.detailId || !detail.fbAccount || !detail.searchUrl) return
  claimedDetailIds.add(String(detail.detailId))
  startBrowserCollect(
    String(detail.detailId),
    detail.fbAccount,
    detail.cookie || null,
    detail.searchUrl,
    detail.expectedCount || 1,
    detail.taskType || 1,
    detail.sourceUserId ? JSON.stringify({ sourceUserId: String(detail.sourceUserId) }) : undefined
  )
}

export const isAiAgentClaimedDetail = (detailId?: string | number) => {
  return !!detailId && claimedDetailIds.has(String(detailId))
}

export const claimAndStartPendingAiAgentDetails = async (forceLimit?: number) => {
  if (polling || !getBridge()) return 0
  const availableSlots = typeof forceLimit === 'number' ? forceLimit : getAvailableSlots()
  if (availableSlots <= 0) return 0

  polling = true
  try {
    const details = await FbAiAgentApi.claimPendingCollectDetails(Math.min(availableSlots, 10))
    ;(details || []).forEach(startAiAgentCollectDetail)
    return details?.length || 0
  } catch (error) {
    console.warn('领取AI获客采集明细失败', error)
    return 0
  } finally {
    polling = false
  }
}

export const claimNextAiAgentDetail = async () => {
  if (polling || !getBridge()) return null

  polling = true
  try {
    const details = await FbAiAgentApi.claimPendingCollectDetails(1)
    const nextDetail = details?.[0]
    return nextDetail || null
  } catch (error) {
    console.warn('领取AI获客下一条采集明细失败', error)
    return null
  } finally {
    polling = false
  }
}

export const setupWpfAiAgentTaskPoller = () => {
  if (timer) return
  void claimAndStartPendingAiAgentDetails()
  timer = window.setInterval(claimAndStartPendingAiAgentDetails, 10000)
}

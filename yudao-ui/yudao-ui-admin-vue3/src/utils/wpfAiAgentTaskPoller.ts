import { FbAiAgentApi, type FbAiAgentDispatchDetail } from '@/api/facebook/aiagent'
import { FbCollectApi } from '@/api/facebook/collect'
import { DmTaskApi } from '@/api/facebook/dmtask'
import { markOperationDetailFailed } from '@/api/facebook/operation'
import { closeBrowser, startBrowserCollect } from '@/utils/wpfBridge'
import { getFbAccountProxyJson } from '@/utils/fbAccountProxy'

let polling = false
const claimedDetailIds = new Set<string>()
const runningAccounts = new Set<string>()
const detailTimeouts = new Map<string, number>()
const finishedDetailIds = new Set<string>()
const queuedDetailSources = new Map<string, string>()
const detailRunningAccounts = new Map<string, string>()
// WPF 帖子搜索会按页面加载进度继续滚动，脚本自身的保护上限是 5 分钟。
// 队列超时必须比脚本上限长，避免真实结果回传时已被本地 finished 标记丢弃。
const DETAIL_TIMEOUT_MS = 6 * 60 * 1000
const FINISHED_DETAIL_KEEP_MS = 10 * 60 * 1000

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

export const startAiAgentCollectDetail = async (
  detail: FbAiAgentDispatchDetail,
  options: { force?: boolean } = {}
) => {
  if (!detail.detailId || !detail.fbAccount) return false
  const account = String(detail.fbAccount)
  const browserAccount = detail.sourceType === 'operation'
    ? String(detail.accountId || detail.fbAccount)
    : account
  if (!options.force && runningAccounts.has(account)) return false
  const detailId = String(detail.detailId)
  claimedDetailIds.add(detailId)
  runningAccounts.add(account)
  detailRunningAccounts.set(detailId, account)
  queuedDetailSources.set(detailId, detail.sourceType || 'collect')
  try {
    if (detail.sourceType === 'dm') {
      const bridge = getBridge()
      if (!bridge?.StartDmTask || !detail.targetUserId || !detail.scriptContent) {
        runningAccounts.delete(account)
        claimedDetailIds.delete(detailId)
        detailRunningAccounts.delete(detailId)
        queuedDetailSources.delete(detailId)
        return false
      }
      const proxyConfigJson = await getFbAccountProxyJson(account)
      bridge.StartDmTask(
        String(detail.taskId || ''),
        String(detail.detailId),
        account,
        detail.cookie || '',
        detail.targetUserId,
        detail.scriptContent,
        detail.password,
        detail.tfa,
        proxyConfigJson
      )
      registerQueuedDetailTimeout(account, detail.detailId, 'dm')
      return true
    }
    if (detail.sourceType === 'operation') {
      const startUrl = detail.searchUrl || detail.actionConfig
        ? resolveOperationStartUrl(detail)
        : ''
      if (!startUrl) {
        runningAccounts.delete(account)
        claimedDetailIds.delete(detailId)
        detailRunningAccounts.delete(detailId)
        queuedDetailSources.delete(detailId)
        return false
      }
      if (Number(detail.taskType) === 13) {
        const bridge = getBridge()
        if (!bridge?.StartGroupPublishTask) {
          runningAccounts.delete(account)
          claimedDetailIds.delete(detailId)
          detailRunningAccounts.delete(detailId)
          queuedDetailSources.delete(detailId)
          return false
        }
        const proxyConfigJson = await getFbAccountProxyJson(browserAccount)
        bridge.StartGroupPublishTask(
          String(detail.taskId || ''),
          browserAccount,
          detail.cookie || '',
          detail.actionConfig || '{}',
          String(detail.detailId),
          detail.password,
          detail.tfa,
          detail.fbAccount,
          proxyConfigJson
        )
        registerQueuedDetailTimeout(account, detail.detailId, 'operation')
        return true
      }
      startBrowserCollect(
        String(detail.detailId),
        browserAccount,
        detail.cookie || null,
        startUrl,
        detail.expectedCount || 1,
        detail.taskType || 10,
        detail.actionConfig,
        true,
        undefined,
        detail.password,
        detail.tfa,
        detail.fbAccount
      )
      registerQueuedDetailTimeout(account, detail.detailId, 'operation')
      return true
    }
    if (!detail.searchUrl) {
      runningAccounts.delete(account)
      claimedDetailIds.delete(detailId)
      detailRunningAccounts.delete(detailId)
      queuedDetailSources.delete(detailId)
      return false
    }
    startBrowserCollect(
      String(detail.detailId),
      detail.fbAccount,
      detail.cookie || null,
      detail.searchUrl,
      detail.expectedCount || 1,
      detail.taskType || 1,
      detail.actionConfig || (detail.sourceUserId ? JSON.stringify({ sourceUserId: String(detail.sourceUserId) }) : undefined),
      false,
      undefined,
      detail.password,
      detail.tfa,
      detail.fbAccount
    )
    registerQueuedDetailTimeout(account, detail.detailId, 'collect')
    return true
  } catch (error) {
    runningAccounts.delete(account)
    claimedDetailIds.delete(detailId)
    detailRunningAccounts.delete(detailId)
    queuedDetailSources.delete(detailId)
    throw error
  }
}

function resolveOperationStartUrl(detail: FbAiAgentDispatchDetail) {
  if (detail.searchUrl) {
    return detail.searchUrl
  }
  if (!detail.actionConfig) {
    return ''
  }
  try {
    const config = JSON.parse(detail.actionConfig)
    const urls =
      config.postUrls ||
      config.groups ||
      config.selectedGroups ||
      config.selectedUnjoinedGroups ||
      config.actionConfig?.groups
    if (Array.isArray(urls) && urls.length > 0) {
      return urls[0]?.postUrl || urls[0]?.groupUrl || urls[0]?.url || String(urls[0] || '')
    }
    return config.postUrl || config.targetUrl || config.actionConfig?.postUrl || config.actionConfig?.targetUrl || ''
  } catch {
    return ''
  }
}

export const isAiAgentClaimedDetail = (detailId?: string | number) => {
  return !!detailId && claimedDetailIds.has(String(detailId))
}

export const markAiAgentCollectFinished = (accountId?: string | number, detailId?: string | number) => {
  if (detailId) {
    const value = String(detailId)
    const runningAccount = detailRunningAccounts.get(value)
    if (runningAccount) {
      runningAccounts.delete(runningAccount)
    }
    claimedDetailIds.delete(value)
    detailRunningAccounts.delete(value)
    queuedDetailSources.delete(value)
    return
  }
  if (accountId) {
    runningAccounts.delete(String(accountId))
  }
}

export const beginQueuedDmResult = (detailId?: string | number) => {
  const value = String(detailId || '')
  if (!value || finishedDetailIds.has(value)) {
    return false
  }
  rememberFinishedDetail(value)
  clearQueuedDetailTimeout(value)
  return true
}

export const beginQueuedDetailResult = (detailId?: string | number) => {
  const value = String(detailId || '')
  if (!value || finishedDetailIds.has(value)) {
    return false
  }
  rememberFinishedDetail(value)
  clearQueuedDetailTimeout(value)
  return true
}

async function handleWpfBrowserClosed(event: Event) {
  const detail = (event as CustomEvent).detail || {}
  const detailId = String(detail.detailId || '')
  const accountId = String(detail.accountId || '')
  const sourceType = queuedDetailSources.get(detailId)
  if (!detailId || !accountId || !sourceType || finishedDetailIds.has(detailId)) return
  // 关闭通知可能先于 WPF 已排队的 collection-complete 事件到达。
  // 此处不能清理 claimedDetailId、标记失败或清除超时，否则正常完成回调会被丢弃。
  // 正常完成由 collection-complete 回调释放账号；异常关闭则由原有超时机制回收。
}

export const finishQueuedAccountTaskAndStartNext = async (
  accountId?: string | number,
  detailId?: string | number
) => {
  markAiAgentCollectFinished(accountId, detailId)
  const nextDetail = await claimNextAiAgentDetail()
  if (nextDetail) {
    startAiAgentCollectDetail(nextDetail)
  }
  return nextDetail || null
}

export function registerQueuedDetailTimeout(accountId: string, detailId: string | number, sourceType: string) {
  const value = String(detailId)
  clearQueuedDetailTimeout(value)
  queuedDetailSources.set(value, sourceType)
  const timeout = window.setTimeout(() => {
    void timeoutQueuedDetail(accountId, value, sourceType)
  }, DETAIL_TIMEOUT_MS)
  detailTimeouts.set(value, timeout)
}

function clearQueuedDetailTimeout(detailId: string | number) {
  const value = String(detailId)
  const timeout = detailTimeouts.get(value)
  if (timeout) {
    window.clearTimeout(timeout)
    detailTimeouts.delete(value)
  }
}

async function timeoutQueuedDetail(accountId: string, detailId: string, sourceType: string) {
  if (finishedDetailIds.has(detailId)) {
    return
  }
  rememberFinishedDetail(detailId)
  detailTimeouts.delete(detailId)
  try {
    if (sourceType === 'dm') {
      await DmTaskApi.reportDetail({
        detailId,
        status: 2,
        errorMsg: '私信发送超过6分钟未回传'
      })
      window.dispatchEvent(new CustomEvent('fb:dm:result:saved', { detail: { detailId } }))
    } else if (sourceType === 'operation') {
      await markOperationDetailFailed({
        detailId,
        errorMsg: '运营执行超过6分钟未回传'
      })
      window.dispatchEvent(new CustomEvent('fb:repost:result:saved', { detail: { detailId } }))
    } else {
      await FbCollectApi.markDetailFailed({
        detailId,
        errorMessage: '采集执行超过6分钟未回传'
      })
      window.dispatchEvent(new CustomEvent('fb:collect:saved', { detail: { detailId } }))
    }
  } catch (error) {
    console.error('队列任务超时失败上报失败', error)
  } finally {
    const nextDetail = await finishQueuedAccountTaskAndStartNext(accountId, detailId)
    const nextAccountId = nextDetail ? String(nextDetail.accountId || nextDetail.fbAccount || '') : ''
    if (!nextDetail || nextAccountId !== accountId) {
      closeBrowser(accountId)
    }
  }
}

function rememberFinishedDetail(detailId: string) {
  finishedDetailIds.add(detailId)
  window.setTimeout(() => finishedDetailIds.delete(detailId), FINISHED_DETAIL_KEEP_MS)
}

export const claimAndStartPendingAiAgentDetails = async (forceLimit?: number) => {
  if (polling || !getBridge()) return 0
  const availableSlots = typeof forceLimit === 'number' ? forceLimit : getAvailableSlots()
  if (availableSlots <= 0) return 0

  polling = true
  try {
    const details = await FbAiAgentApi.claimPendingCollectDetails(
      Math.min(availableSlots, 10),
      Array.from(runningAccounts)
    )
    let started = 0
    const startedAccounts = new Set<string>()
    for (const detail of details || []) {
      const account = String(detail.fbAccount || '')
      if (!account || runningAccounts.has(account) || startedAccounts.has(account)) {
        continue
      }
      if (await startAiAgentCollectDetail(detail)) {
        startedAccounts.add(account)
        started++
      }
    }
    return started
  } catch (error) {
    console.warn('领取AI获客采集明细失败', error)
    return 0
  } finally {
    polling = false
  }
}

export const claimNextAiAgentDetailInTask = async (accountId?: string | number, taskId?: string | number) => {
  if (!accountId || !taskId || !getBridge()) return null
  try {
    return await FbAiAgentApi.claimNextCollectDetail(String(accountId), String(taskId))
  } catch (error) {
    console.warn('领取AI获客当前任务下一条采集明细失败', error)
    return null
  }
}

export const claimNextAiAgentDetail = async () => {
  if (polling || !getBridge()) return null

  polling = true
  try {
    const details = await FbAiAgentApi.claimPendingCollectDetails(1, Array.from(runningAccounts))
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
  window.addEventListener('fb:wpf:browser-closed', (event) => { void handleWpfBrowserClosed(event) })
  // 任务由 Vue 调用后端领取，WPF 只接收已领取的明细并操作浏览器。
  // 首屏完成后补领一次，覆盖 WPF/Vue 启动前已经创建的任务。
  window.setTimeout(() => {
    void claimAndStartPendingAiAgentDetails()
  }, 1500)
  window.setInterval(() => {
    void claimAndStartPendingAiAgentDetails()
  }, 30_000)
}

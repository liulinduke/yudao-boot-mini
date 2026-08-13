import { DmTaskApi } from '@/api/facebook/dmtask'
import { FbCollectGroupApi } from '@/api/facebook/fbcollectgroup'
import { FbCollectPostApi } from '@/api/facebook/fbcollectpost'
import { FbCollectUserApi } from '@/api/facebook/collectuser'
import { batchSaveAddGroupResult, batchSaveRepostResult, markOperationDetailSuccess } from '@/api/facebook/operation'
import {
  beginQueuedDetailResult,
  beginQueuedDmResult,
  claimNextAiAgentDetail,
  finishQueuedAccountTaskAndStartNext,
  isAiAgentClaimedDetail,
  markAiAgentCollectFinished,
  registerQueuedDetailTimeout,
  startAiAgentCollectDetail
} from '@/utils/wpfAiAgentTaskPoller'
import { closeBrowser, onCollectionBatch, onCollectionComplete } from '@/utils/wpfBridge'
import { onCollectionError } from '@/utils/wpfBridge'
import { FbAccountApi } from '@/api/facebook/account'
import { FbCollectApi } from '@/api/facebook/collect'
import { isWarmupDetail, reportWarmupDetail } from '@/utils/wpfWarmupTaskPoller'

const handledCollectDetailIds = new Set<string>()
const handledDmDetailIds = new Set<string>()
const handledGroupPublishDetailIds = new Set<string>()
const handledRepostDetailIds = new Set<string>()
const handledPublishPostDetailIds = new Set<string>()
const collectBatchChains = new Map<string, Promise<void>>()
const savedCollectBatchCounts = new Map<string, number>()
let initialized = false

function parseResultList(raw: unknown): any[] {
  if (Array.isArray(raw)) {
    return raw
  }
  if (typeof raw === 'string') {
    try {
      const parsed = JSON.parse(raw)
      return Array.isArray(parsed) ? parsed : []
    } catch {
      return []
    }
  }
  return []
}

/**
 * 全局监听 WPF 运营任务结果并上报后端
 */
export function setupWpfOperationSync() {
  if (initialized) {
    return
  }
  initialized = true

  onCollectionComplete(async (data) => {
    if (Number(data.taskType) === 17 && isWarmupDetail(data.detailId)) {
      try {
        const result = typeof data.results === 'string' ? JSON.parse(data.results || '{}') : (data.results || {})
        await reportWarmupDetail(String(data.detailId), result.success !== false, result.message)
        window.dispatchEvent(new CustomEvent('fb:warmup:saved', { detail: { detailId: data.detailId } }))
      } catch (error) {
        console.error('[养号定时任务] 完成状态回报失败:', error)
      }
      return
    }
    if (isAiAgentClaimedDetail(data.detailId) && isCollectTaskType(data.taskType)) {
      await saveCollectResult(data)
      return
    }
    if (data.taskType === 10 || data.taskType === 15 || data.taskType === 16) {
      await saveRepostResult(data)
      return
    }
    if (data.taskType === 13) {
      await saveGroupPublishResult(data)
      return
    }
    if (data.taskType === 12) {
      await savePublishPostResult(data)
      return
    }
    if (data.taskType !== 14) {
      return
    }
    const result = data.results
    if (!result || typeof result !== 'object') {
      console.warn('[私信结果] 数据格式无效:', data)
      return
    }

    const detailId = String(result.detailId || data.detailId || '')
    if (!detailId || handledDmDetailIds.has(detailId) || !beginQueuedDmResult(detailId)) {
      return
    }
    handledDmDetailIds.add(detailId)
    const accountId = String(data.accountId || result.accountId || '')

    try {
      console.log('[私信结果] 上报后端:', { detailId, success: result.success })
      await DmTaskApi.reportDetail({
        detailId,
        status: result.success ? 1 : 2,
        errorMsg: result.message || ''
      })
      window.dispatchEvent(new CustomEvent('fb:dm:result:saved', { detail: { detailId } }))
    } catch (error) {
      console.error('[私信结果] 上报失败:', error)
    } finally {
      if (accountId) {
        await finishQueuedAccountTaskAndStartNext(accountId, detailId)
      }
    }
  })

  onCollectionBatch((data) => {
    if (!isAiAgentClaimedDetail(data.detailId) || !isCollectTaskType(data.taskType)) return
    const detailId = String(data.detailId || '')
    const results = parseResultList(data.results)
    if (!detailId || results.length === 0) return
    const previous = collectBatchChains.get(detailId) || Promise.resolve()
    const chain = previous.then(async () => {
      await saveCollectedItems(detailId, Number(data.taskType || 1), results)
      savedCollectBatchCounts.set(detailId, (savedCollectBatchCounts.get(detailId) || 0) + results.length)
      // 有持续数据回传说明脚本仍在正常工作，延长本明细的无响应保护时间。
      registerQueuedDetailTimeout(String(data.accountId || ''), detailId, 'collect')
    })
    collectBatchChains.set(detailId, chain)
    void chain.catch((error) => console.error('[AI获客采集批次] 上报失败:', error))
  })

  onCollectionError(async (data) => {
    const reason = String(data.errorMessage || '')
    const detailId = String(data.detailId || '')
    if (Number(data.taskType) === 17 && isWarmupDetail(detailId)) {
      try {
        await reportWarmupDetail(detailId, false, reason || '养号任务失败')
      } catch (error) {
        console.error('[养号定时任务] 失败状态回报失败:', error)
      }
      return
    }
    // 资料上传使用 profile_profile_* 业务明细，不属于采集明细，不能提交到 collect-detail/fail。
    if (/^profile_/i.test(detailId)) return
    const accountId = String(data.accountId || '')
    if (detailId && accountId && !/账号正在执行任务|当前明细/.test(reason)) {
      try {
        await FbCollectApi.markDetailFailed({
          detailId,
          errorMessage: reason || '浏览器加载失败'
        })
        await finishQueuedAccountTaskAndStartNext(accountId, detailId)
        window.dispatchEvent(new CustomEvent('fb:collect:saved', { detail: { detailId } }))
      } catch (error) {
        console.error('[采集异常] 明细失败状态保存失败:', error)
      }
    }
    if (!/cookie|登录页|重新登录|checkpoint|账号被封/i.test(reason) || !data.accountId) return

    try {
      const page = await FbAccountApi.getFbAccountPage({
        pageNo: 1,
        pageSize: 1,
        fbAccount: String(data.accountId)
      })
      const account = page?.list?.[0]
      if (!account?.id) return
      await FbAccountApi.updateFbAccountLoginResult({
        id: account.id,
        loginStatus: 'COOKIE_INVALID',
        loginErrorReason: reason
      })
      window.dispatchEvent(new CustomEvent('fb:account:status:changed', {
        detail: { accountId: String(data.accountId), loginStatus: 'COOKIE_INVALID', errorMessage: reason }
      }))
    } catch (error) {
      console.error('[账号登录状态] Cookie失效状态保存失败:', error)
    }
  })
}

function isCollectTaskType(taskType?: number) {
  return [1, 2, 3, 4, 6, 7, 8, 11, 12].includes(Number(taskType || 1))
}

function parseMetricNumber(raw: string): number | null {
  if (!raw) return null
  try {
    const normalized = raw.replace(',', '.')
    const numberPart = normalized.replace(/[^\d.]/g, '')
    if (!numberPart) return null

    let number = parseFloat(numberPart)
    const lower = raw.toLowerCase()
    if (lower.includes('rb') || lower.includes('ribu') || lower.includes('rbu') || lower.includes('천')) {
      number *= 1000
    } else if (lower.includes('jt') || lower.includes('juta') || lower.includes('만') || lower.includes('万')) {
      number *= 10000
    } else if (lower.includes('백만') || lower.includes('百万') || lower.includes('m')) {
      number *= 1000000
    } else if (lower.includes('b')) {
      number *= 1000000000
    } else if (lower.includes('k')) {
      number *= 1000
    }
    const parsed = Math.floor(number)
    return parsed > 0 && parsed <= 1000000000 ? parsed : null
  } catch {
    return null
  }
}

async function saveCollectResult(data: any) {
  const detailId = String(data.detailId || '')
  if (!detailId || handledCollectDetailIds.has(detailId) || !beginQueuedDetailResult(detailId)) {
    return
  }

  const taskType = Number(data.taskType || 1)
  await (collectBatchChains.get(detailId) || Promise.resolve())
  const reportedCount = savedCollectBatchCounts.get(detailId) || 0
  const results = parseResultList(data.results).slice(reportedCount)
  handledCollectDetailIds.add(detailId)
  try {
    await saveCollectedItems(detailId, taskType, results)
    markAiAgentCollectFinished(data.accountId, detailId)
    const nextDetail = await claimNextAiAgentDetail()
    if (nextDetail) {
      startAiAgentCollectDetail(nextDetail)
    }
    window.dispatchEvent(new CustomEvent('fb:ai-agent:collect:saved', { detail: { detailId, taskType } }))
    window.dispatchEvent(new CustomEvent('fb:collect:saved', { detail: { detailId, taskType } }))
    collectBatchChains.delete(detailId)
    savedCollectBatchCounts.delete(detailId)
  } catch (error) {
    handledCollectDetailIds.delete(detailId)
    console.error('[AI获客采集结果] 上报失败:', error)
  }
}

async function saveCollectedItems(detailId: string, taskType: number, results: any[]) {
  if (results.length === 0) return
  if (taskType === 2) {
    await FbCollectPostApi.batchSaveFbCollectPost({ detailId: detailId as any, results: results.map((item: any) => ({ ...item, reshareCount: typeof item.reshareCount === 'string' ? parseMetricNumber(item.reshareCount) : item.reshareCount, commentCount: typeof item.commentCount === 'string' ? parseMetricNumber(item.commentCount) : item.commentCount, reactionCount: typeof item.reactionCount === 'string' ? parseMetricNumber(item.reactionCount) : item.reactionCount })) })
  } else if (taskType === 4) {
    await FbCollectGroupApi.batchSaveFbCollectGroup({ detailId: detailId as any, results: results.map((item: any) => ({ ...item, memberQuantity: typeof item.memberQuantity === 'string' ? parseMetricNumber(item.memberQuantity) : item.memberQuantity })) })
  } else {
    await FbCollectUserApi.batchSaveFbCollectUser({ detailId: detailId as any, results: results.map((item: any) => ({ ...item, followers: typeof item.followers === 'string' ? parseMetricNumber(item.followers) : item.followers })) })
  }
}

async function saveGroupPublishResult(data: any) {
  const detailId = String(data.detailId || '')
  if (!detailId || handledGroupPublishDetailIds.has(detailId) || !beginQueuedDetailResult(detailId)) {
    return
  }

  const results = parseResultList(data.results)
  if (results.length === 0) {
    console.warn('[发群帖结果] 结果为空，跳过保存', data)
    return
  }

  handledGroupPublishDetailIds.add(detailId)
  try {
    console.log('[发群帖结果] 上报后端:', { detailId, count: results.length })
    await batchSaveAddGroupResult({
      detailId,
      results: results.map((item: any) => ({
        ...item,
        accountId: String(item.accountId || data.accountId || ''),
        joinStatus: item.joinStatus ?? (item.success ? 1 : 2)
      }))
    })
    window.dispatchEvent(
      new CustomEvent('fb:group-publish:result:saved', { detail: { detailId } })
    )
    const nextDetail = await finishQueuedAccountTaskAndStartNext(data.accountId, detailId)
    const nextAccountId = nextDetail
      ? String(nextDetail.accountId || nextDetail.fbAccount || '')
      : ''
    if (!nextDetail || nextAccountId !== String(data.accountId)) {
      closeBrowser(String(data.accountId))
    }
  } catch (error) {
    handledGroupPublishDetailIds.delete(detailId)
    console.error('[发群帖结果] 上报失败:', error)
  }
}

async function savePublishPostResult(data: any) {
  const detailId = String(data.detailId || '')
  if (!detailId || handledPublishPostDetailIds.has(detailId)) {
    return
  }

  handledPublishPostDetailIds.add(detailId)
  const accountId = String(data.accountId || '')
  try {
    const result = data.results && typeof data.results === 'object' ? data.results : {}
    if (result.success === false) {
      throw new Error(result.message || '发个人帖失败')
    }
    await markOperationDetailSuccess({
      detailId,
      actualCount: Number(result.actualCount || 1)
    })
    window.dispatchEvent(new CustomEvent('fb:publish-post:result:saved', { detail: { detailId } }))
    const nextDetail = await finishQueuedAccountTaskAndStartNext(accountId, detailId)
    const nextAccountId = nextDetail
      ? String(nextDetail.accountId || nextDetail.fbAccount || '')
      : ''
    if (accountId && (!nextDetail || nextAccountId !== accountId)) {
      closeBrowser(accountId)
    }
  } catch (error) {
    handledPublishPostDetailIds.delete(detailId)
    console.error('[发个人帖结果] 上报失败:', error)
  }
}

async function saveRepostResult(data: any) {
  const detailId = String(data.detailId || '')
  if (!detailId || handledRepostDetailIds.has(detailId) || !beginQueuedDetailResult(detailId)) {
    return
  }

  const rawResults = parseResultList(data.results)
  const results =
    rawResults.length > 0
      ? rawResults
      : [
          {
            accountId: String(data.accountId || ''),
            status: 2,
            failReason: '转帖任务已结束，但未返回任何结果'
          }
        ]

  handledRepostDetailIds.add(detailId)
  try {
    console.log('[转帖结果] 上报后端:', { detailId, count: results.length })
    await batchSaveRepostResult({
      detailId,
      results: results.map((item: any) => ({
        ...item,
        accountId: String(item.accountId || data.accountId || ''),
        status: item.status ?? (item.success ? 1 : 2)
      }))
    })
    window.dispatchEvent(new CustomEvent('fb:repost:result:saved', { detail: { detailId } }))
    await finishQueuedAccountTaskAndStartNext(data.accountId, detailId)
  } catch (error) {
    handledRepostDetailIds.delete(detailId)
    console.error('[转帖结果] 上报失败:', error)
  }
}

import { DmTaskApi } from '@/api/facebook/dmtask'
import { FbCollectGroupApi } from '@/api/facebook/fbcollectgroup'
import { FbCollectPostApi } from '@/api/facebook/fbcollectpost'
import { FbCollectUserApi } from '@/api/facebook/collectuser'
import { batchSaveAddGroupResult, batchSaveRepostResult } from '@/api/facebook/operation'
import {
  beginQueuedDetailResult,
  beginQueuedDmResult,
  claimNextAiAgentDetail,
  finishQueuedAccountTaskAndStartNext,
  isAiAgentClaimedDetail,
  markAiAgentCollectFinished,
  startAiAgentCollectDetail
} from '@/utils/wpfAiAgentTaskPoller'
import { closeBrowser, onCollectionComplete } from '@/utils/wpfBridge'

const handledCollectDetailIds = new Set<string>()
const handledDmDetailIds = new Set<string>()
const handledGroupPublishDetailIds = new Set<string>()
const handledRepostDetailIds = new Set<string>()
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
  const results = parseResultList(data.results)
  handledCollectDetailIds.add(detailId)
  try {
    if (taskType === 2) {
      await FbCollectPostApi.batchSaveFbCollectPost({
        detailId: detailId as any,
        results: results.map((item: any) => ({
          ...item,
          reshareCount:
            typeof item.reshareCount === 'string' ? parseMetricNumber(item.reshareCount) : item.reshareCount,
          commentCount:
            typeof item.commentCount === 'string' ? parseMetricNumber(item.commentCount) : item.commentCount,
          reactionCount:
            typeof item.reactionCount === 'string' ? parseMetricNumber(item.reactionCount) : item.reactionCount
        }))
      })
    } else if (taskType === 4) {
      await FbCollectGroupApi.batchSaveFbCollectGroup({
        detailId: detailId as any,
        results: results.map((item: any) => ({
          ...item,
          memberQuantity:
            typeof item.memberQuantity === 'string' ? parseMetricNumber(item.memberQuantity) : item.memberQuantity
        }))
      })
    } else {
      await FbCollectUserApi.batchSaveFbCollectUser({
        detailId: detailId as any,
        results: results.map((item: any) => ({
          ...item,
          followers: typeof item.followers === 'string' ? parseMetricNumber(item.followers) : item.followers
        }))
      })
    }
    markAiAgentCollectFinished(data.accountId, detailId)
    const nextDetail = await claimNextAiAgentDetail()
    if (data.accountId && (!nextDetail || String(nextDetail.fbAccount) !== String(data.accountId))) {
      closeBrowser(String(data.accountId))
    }
    if (nextDetail) {
      startAiAgentCollectDetail(nextDetail)
    }
    window.dispatchEvent(new CustomEvent('fb:ai-agent:collect:saved', { detail: { detailId, taskType } }))
    window.dispatchEvent(new CustomEvent('fb:collect:saved', { detail: { detailId, taskType } }))
  } catch (error) {
    handledCollectDetailIds.delete(detailId)
    console.error('[AI获客采集结果] 上报失败:', error)
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
    await finishQueuedAccountTaskAndStartNext(data.accountId, detailId)
  } catch (error) {
    handledGroupPublishDetailIds.delete(detailId)
    console.error('[发群帖结果] 上报失败:', error)
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

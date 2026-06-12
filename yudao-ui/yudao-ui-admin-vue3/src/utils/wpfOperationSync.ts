import { DmTaskApi } from '@/api/facebook/dmtask'
import { batchSaveAddGroupResult, batchSaveRepostResult } from '@/api/facebook/operation'
import { onCollectionComplete } from '@/utils/wpfBridge'

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
    if (data.taskType === 10) {
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
    if (!detailId || handledDmDetailIds.has(detailId)) {
      return
    }
    handledDmDetailIds.add(detailId)

    try {
      console.log('[私信结果] 上报后端:', { detailId, success: result.success })
      await DmTaskApi.reportDetail({
        detailId,
        status: result.success ? 1 : 2,
        errorMsg: result.message || ''
      })
      window.dispatchEvent(new CustomEvent('fb:dm:result:saved', { detail: { detailId } }))
    } catch (error) {
      handledDmDetailIds.delete(detailId)
      console.error('[私信结果] 上报失败:', error)
    }
  })
}

async function saveGroupPublishResult(data: any) {
  const detailId = String(data.detailId || '')
  if (!detailId || handledGroupPublishDetailIds.has(detailId)) {
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
  } catch (error) {
    handledGroupPublishDetailIds.delete(detailId)
    console.error('[发群帖结果] 上报失败:', error)
  }
}

async function saveRepostResult(data: any) {
  const detailId = String(data.detailId || '')
  if (!detailId || handledRepostDetailIds.has(detailId)) {
    return
  }

  const results = parseResultList(data.results)
  if (results.length === 0) {
    console.warn('[转帖结果] 结果为空，跳过保存:', data)
    return
  }

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
  } catch (error) {
    handledRepostDetailIds.delete(detailId)
    console.error('[转帖结果] 上报失败:', error)
  }
}

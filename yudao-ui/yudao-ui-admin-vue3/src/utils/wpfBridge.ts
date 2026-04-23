/**
 * WPF CefSharp 桥接工具
 * 仅负责与 WPF BrowserMatrixWindow 通信
 */

// 扩展 Window 接口以支持 CefSharp
declare global {
  interface Window {
    chrome?: {
      webview?: {
        hostObjects?: {
          sync?: {
            wpfBridge?: {
              StartBrowser: (
                taskId: string,
                accountId: string,
                cookie: string | null,
                url: string,
                expectedCount: number,
                taskType: number
              ) => void
            }
          }
        }
        addEventListener: (event: string, callback: (data: any) => void) => void
      }
    }
  }
}

export {}

/**
 * 启动浏览器进行自动化采集
 * @param taskId 任务ID
 * @param accountId 账号ID(fbAccount)
 * @param cookie Cookie字符串
 * @param url 采集URL
 * @param expectedCount 期望采集数量
 * @param taskType 任务类型(1主页/2帖子/3用户/4群组/5活动/6评论)
 */
export function startBrowserCollect(
  taskId: string,
  accountId: string,
  cookie: string | null,
  url: string,
  expectedCount: number,
  taskType: number = 1
): void {
  try {
    // 检查是否在 WPF 环境中
    if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      window.chrome.webview.hostObjects.sync.wpfBridge.StartBrowser(
        taskId,
        accountId,
        cookie,
        url,
        expectedCount,
        taskType
      )
      console.log(`已启动浏览器自动化采集 - 任务ID: ${taskId}, 账号ID: ${accountId}, URL: ${url}, 类型: ${taskType}`)
    } else {
      console.warn('WPF 桥接未就绪，请在 WPF 环境中运行')
    }
  } catch (error) {
    console.error('启动浏览器失败:', error)
    throw error
  }
}

/**
 * 监听采集完成事件（CustomEvent）
 * @param callback 采集完成回调函数
 */
export function onCollectionComplete(callback: (data: any) => void): void {
  try {
    console.log('🔍 注册 CustomEvent 监听器...')
    
    window.addEventListener('fb:collection:complete', (event: any) => {
      console.log('📨 收到采集完成事件:', event)
      const { taskId, accountId, data, timestamp } = event.detail
      console.log('📊 明细ID(taskId):', taskId)
      console.log('📊 账号ID:', accountId)
      console.log('📊 数据条数:', Array.isArray(data) ? data.length : 0)
      
      // 构建统一的数据格式
      const formattedData = {
        type: 'CollectionComplete',
        detailId: taskId,  // WPF返回的taskId实际是detailId
        accountId: accountId,
        results: data,
        count: Array.isArray(data) ? data.length : 0,
        timestamp: timestamp
      }
      
      callback(formattedData)
    })
    
    console.log('✅ 已注册采集完成事件监听')
  } catch (error) {
    console.error('❌ 注册事件监听失败:', error)
  }
}

/**
 * 关闭指定账号的浏览器
 * @param accountId 账号ID(fbAccount)
 */
export function closeBrowser(accountId: string): void {
  try {
    if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      // TODO: WPF 需要添加 CloseBrowser 方法
      // window.chrome.webview.hostObjects.sync.wpfBridge.CloseBrowser(accountId)
      console.log(`请求关闭浏览器 - 账号ID: ${accountId}`)
    } else {
      console.warn('WPF 桥接未就绪，请在 WPF 环境中运行')
    }
  } catch (error) {
    console.error('关闭浏览器失败:', error)
    throw error
  }
}

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
                accountId: number,
                cookie: string | null,
                url: string,
                expectedCount: number
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
 * @param accountId 账号ID
 * @param cookie Cookie字符串
 * @param url 采集URL
 * @param expectedCount 期望采集数量
 */
export function startBrowserCollect(
  accountId: number,
  cookie: string | null,
  url: string,
  expectedCount: number
): void {
  try {
    // 检查是否在 WPF 环境中
    if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      window.chrome.webview.hostObjects.sync.wpfBridge.StartBrowser(
        accountId,
        cookie,
        url,
        expectedCount
      )
      console.log(`已启动浏览器自动化采集 - 账号ID: ${accountId}, URL: ${url}`)
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
      const { accountId, data, timestamp } = event.detail
      console.log('📊 任务ID:', accountId)
      console.log('📊 数据条数:', Array.isArray(data) ? data.length : 0)
      
      // 构建统一的数据格式
      const formattedData = {
        type: 'CollectionComplete',
        taskId: accountId,
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

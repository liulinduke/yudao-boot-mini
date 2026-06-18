 <template>
  <ContentWrap>
    <div class="mb-4">
      <span class="text-lg font-bold">FB全局配置</span>
    </div>

    <el-form :model="formData" label-width="150px" class="max-w-600px">
        <el-form-item label="每日私信次数">
          <el-input-number
            v-model="formData.dm_daily_limit"
            :min="1"
            :max="1000"
            class="!w-200px"
          />
          <span class="ml-10px text-gray-500">每个账号每天最多可发送的私信数量</span>
        </el-form-item>

        <el-form-item label="每日转帖次数">
          <el-input-number
            v-model="formData.repost_daily_limit"
            :min="1"
            :max="1000"
            class="!w-200px"
          />
          <span class="ml-10px text-gray-500">每个账号每天最多可转帖的数量</span>
        </el-form-item>

        <el-form-item label="每日加组次数">
          <el-input-number
            v-model="formData.join_group_daily_limit"
            :min="1"
            :max="1000"
            class="!w-200px"
          />
          <span class="ml-10px text-gray-500">每个账号每天最多可加入的群组数量</span>
        </el-form-item>

        <el-form-item label="每日评论次数">
          <el-input-number
            v-model="formData.comment_daily_limit"
            :min="1"
            :max="5000"
            class="!w-200px"
          />
          <span class="ml-10px text-gray-500">每个账号每天最多可评论的数量</span>
        </el-form-item>

        <el-divider />

        <el-form-item label="指纹浏览器配置">
          <div class="flex flex-col gap-8px">
            <el-checkbox v-model="formData.browser_disable_images">
              不加载图片（提升性能，减少流量）
            </el-checkbox>
            <el-checkbox v-model="formData.browser_disable_videos">
              不加载视频（提升性能，减少流量）
            </el-checkbox>
            <div class="mt-4px flex items-center">
              <el-input-number
                v-model="formData.browser_max_concurrent"
                :min="1"
                :max="50"
                class="!w-200px"
              />
              <span class="ml-10px text-gray-500 whitespace-nowrap">最大并发窗口数（每个窗口约占用300MB内存）</span>
            </div>
            <div class="text-xs text-gray-400 ml-0px">
              💡 建议值：8GB内存 → 19个窗口 | 16GB内存 → 38个窗口 | 当前系统 {{ Math.floor(getSystemMemory() / 1024) }}GB → {{ getRecommendedConcurrent() }}个
            </div>
          </div>
        </el-form-item>

        <el-form-item>
          <el-button type="primary" @click="handleSubmit" :loading="loading">保存配置</el-button>
        </el-form-item>
      </el-form>
  </ContentWrap>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { GlobalConfigApi } from '@/api/facebook/globalconfig'
import { useMessage } from '@/hooks/web/useMessage'

const message = useMessage()
const loading = ref(false)

/** 获取系统内存（MB）*/
const getSystemMemory = (): number => {
  // @ts-ignore
  if (navigator.deviceMemory) {
    // @ts-ignore
    return navigator.deviceMemory * 1024
  }
  // 默认返回 8GB
  return 8192
}

/** 计算推荐并发数（每窗口300MB）*/
const getRecommendedConcurrent = (): number => {
  const memoryMB = getSystemMemory()
  const perWindowMB = 300
  // 预留 30% 内存给系统和其他应用
  const availableMemory = memoryMB * 0.7
  const recommended = Math.floor(availableMemory / perWindowMB)
  return Math.min(Math.max(recommended, 1), 50) // 限制在 1-50 之间
}

const formData = reactive({
  dm_daily_limit: 100,
  repost_daily_limit: 50,
  join_group_daily_limit: 30,
  comment_daily_limit: 200,
  browser_disable_images: true,
  browser_disable_videos: true,
  browser_max_concurrent: getRecommendedConcurrent()  // 默认使用当前系统推荐值
})

/** 加载配置 */
const loadConfigs = async () => {
  try {
    const res = await GlobalConfigApi.getAllConfigs()
    if (res && res.length > 0) {
      res.forEach((item: any) => {
        if (item.configKey in formData) {
          const key = item.configKey as keyof typeof formData
          const value = item.configValue
          
          // 根据字段类型进行转换
          if (key === 'browser_disable_images' || key === 'browser_disable_videos') {
            // 布尔值字段
            formData[key] = value === 'true'
          } else if (key === 'browser_max_concurrent') {
            // 数字字段
            formData[key] = parseInt(value) || 12
          } else {
            // 其他数字字段
            formData[key] = parseInt(value) || 0
          }
        }
      })
    }
  } catch (error) {
    console.error('加载配置失败:', error)
  }
}

/** 提交表单 */
const handleSubmit = async () => {
  loading.value = true
  try {
    const configs = Object.entries(formData).map(([key, value]) => ({
      configKey: key,
      configValue: String(value),
      description: getConfigDescription(key)
    }))
    
    await GlobalConfigApi.batchSaveConfigs(configs)
    message.success('保存成功')
    
    // 同步配置到 WPF（立即生效）
    syncConfigToWpf()
  } catch (error) {
    message.error('保存失败')
  } finally {
    loading.value = false
  }
}

/** 同步配置到 WPF */
const syncConfigToWpf = () => {
  // @ts-ignore
  if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
    try {
      // @ts-ignore
      window.chrome.webview.hostObjects.sync.wpfBridge.UpdateGlobalConfig(
        formData.browser_disable_images,
        formData.browser_disable_videos,
        formData.browser_max_concurrent
      )
      console.log('✅ 配置已同步到 WPF')
    } catch (error) {
      console.warn('⚠️ 同步配置到 WPF 失败:', error)
    }
  } else {
    console.warn('⚠️ WPF 桥接对象不存在，跳过同步')
  }
}

/** 获取配置描述 */
const getConfigDescription = (key: string) => {
  const descriptions: Record<string, string> = {
    dm_daily_limit: '每日私信次数限制',
    repost_daily_limit: '每日转帖次数限制',
    join_group_daily_limit: '每日加组次数限制',
    comment_daily_limit: '每日评论次数限制',
    browser_disable_images: '指纹浏览器-不加载图片',
    browser_disable_videos: '指纹浏览器-不加载视频',
    browser_max_concurrent: '指纹浏览器-最大并发窗口数'
  }
  return descriptions[key] || ''
}

onMounted(() => {
  loadConfigs()
})
</script>

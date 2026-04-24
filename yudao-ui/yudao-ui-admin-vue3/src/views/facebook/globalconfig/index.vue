<template>
  <ContentWrap>
    <el-card>
      <template #header>
        <div class="flex justify-between items-center">
          <span class="text-lg font-bold">FB全局配置</span>
        </div>
      </template>

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

        <el-form-item>
          <el-button type="primary" @click="handleSubmit" :loading="loading">保存配置</el-button>
        </el-form-item>
      </el-form>
    </el-card>
  </ContentWrap>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { GlobalConfigApi } from '@/api/facebook/globalconfig'
import { useMessage } from '@/hooks/web/useMessage'

const message = useMessage()
const loading = ref(false)

const formData = reactive({
  dm_daily_limit: 100,
  repost_daily_limit: 50,
  join_group_daily_limit: 30,
  comment_daily_limit: 200
})

/** 加载配置 */
const loadConfigs = async () => {
  try {
    const res = await GlobalConfigApi.getAllConfigs()
    if (res && res.length > 0) {
      res.forEach((item: any) => {
        if (item.configKey in formData) {
          formData[item.configKey as keyof typeof formData] = parseInt(item.configValue) || 0
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
  } catch (error) {
    message.error('保存失败')
  } finally {
    loading.value = false
  }
}

/** 获取配置描述 */
const getConfigDescription = (key: string) => {
  const descriptions: Record<string, string> = {
    dm_daily_limit: '每日私信次数限制',
    repost_daily_limit: '每日转帖次数限制',
    join_group_daily_limit: '每日加组次数限制',
    comment_daily_limit: '每日评论次数限制'
  }
  return descriptions[key] || ''
}

onMounted(() => {
  loadConfigs()
})
</script>

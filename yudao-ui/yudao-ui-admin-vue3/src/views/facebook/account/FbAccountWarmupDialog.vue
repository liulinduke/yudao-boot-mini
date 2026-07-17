<template>
  <el-dialog v-model="visible" title="养号" width="560px" append-to-body>
    <el-form label-width="110px">
      <el-form-item label="养号动作" required>
        <el-checkbox-group v-model="form.actions">
          <el-checkbox label="feed_scroll">主页随机滚动</el-checkbox>
          <el-checkbox label="safe_click">随机点击页面</el-checkbox>
          <el-checkbox label="friend_profile">浏览好友主页</el-checkbox>
          <el-checkbox label="reels">浏览 Reels</el-checkbox>
        </el-checkbox-group>
      </el-form-item>

      <el-form-item label="运行时长">
        <el-input-number v-model="form.durationMinutes" :min="1" :max="1440" />
        <span class="ml-2 text-gray-500">分钟</span>
      </el-form-item>

      <el-form-item label="页面停留">
        <el-input-number v-model="form.minStaySeconds" :min="3" :max="3600" />
        <span class="mx-2">至</span>
        <el-input-number v-model="form.maxStaySeconds" :min="3" :max="3600" />
        <span class="ml-2 text-gray-500">秒</span>
      </el-form-item>

      <el-form-item v-if="form.actions.includes('friend_profile')" label="好友主页数">
        <el-input-number v-model="form.maxFriendProfiles" :min="1" :max="100" />
      </el-form-item>

      <el-form-item v-if="form.actions.includes('reels')" label="Reels数量">
        <el-input-number v-model="form.maxReels" :min="1" :max="500" />
      </el-form-item>

      <el-form-item v-if="form.actions.includes('reels')" label="随机点赞">
        <el-switch v-model="form.enableLike" />
        <el-input-number
          v-if="form.enableLike"
          v-model="form.likeProbability"
          class="ml-3"
          :min="1"
          :max="100"
        />
        <span v-if="form.enableLike" class="ml-2">%</span>
      </el-form-item>

      <el-form-item label="执行账号">
        <el-tag v-for="account in accounts" :key="String(account.id)" class="mr-2 mb-1">
          {{ account.fbAccount || account.id }}
        </el-tag>
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="starting" @click="handleStart">开始养号</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { startBrowserCollect } from '@/utils/wpfBridge'

interface AccountItem {
  id: string | number
  fbAccount?: string
  cookie?: string
  deviceId?: string | number
}

const visible = ref(false)
const starting = ref(false)
const accounts = ref<AccountItem[]>([])
const form = reactive({
  actions: ['feed_scroll', 'safe_click'] as string[],
  durationMinutes: 20,
  minStaySeconds: 15,
  maxStaySeconds: 45,
  maxFriendProfiles: 5,
  maxReels: 20,
  enableLike: false,
  likeProbability: 0
})

const open = (selectedAccounts: AccountItem[]) => {
  accounts.value = selectedAccounts
  visible.value = true
}

const handleStart = async () => {
  if (!form.actions.length) {
    ElMessage.warning('请至少选择一个养号动作')
    return
  }
  if (form.minStaySeconds > form.maxStaySeconds) {
    ElMessage.warning('最短停留时间不能大于最长停留时间')
    return
  }
  if (!accounts.value.length) {
    ElMessage.warning('请先选择账号')
    return
  }

  starting.value = true
  try {
    const config = JSON.stringify({
      actions: form.actions,
      durationMinutes: form.durationMinutes,
      minStaySeconds: form.minStaySeconds,
      maxStaySeconds: form.maxStaySeconds,
      maxFriendProfiles: form.maxFriendProfiles,
      maxReels: form.maxReels,
      enableLike: form.enableLike,
      likeProbability: form.enableLike ? form.likeProbability : 0
    })

    accounts.value.forEach((account) => {
      startBrowserCollect(
        `warmup-${Date.now()}-${String(account.id)}`,
        account.fbAccount || String(account.id),
        account.cookie || null,
        'https://www.facebook.com',
        0,
        17,
        config,
        true,
        account.deviceId == null ? undefined : String(account.deviceId)
      )
    })
    ElMessage.success(`已提交 ${accounts.value.length} 个账号的养号任务`)
    visible.value = false
  } catch (error) {
    console.error('启动养号失败', error)
    ElMessage.error('启动养号失败，请确认 WPF 桥接已连接')
  } finally {
    starting.value = false
  }
}

defineExpose({ open })
</script>

<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="500px">
    <el-form :model="formData" label-width="100px">
      <el-form-item label="选择代理">
        <el-select
          v-model="formData.proxyId"
          placeholder="请选择代理"
          class="w-full"
        >
          <el-option :value="null" label="不设置代理" />
          <el-option
            v-for="proxy in proxyList"
            :key="proxy.id"
            :value="proxy.id"
            :label="proxy.proxyName"
          />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-text type="info" size="small">
          <template #default>
            将为选中的 {{ selectedCount }} 个账号设置代理
          </template>
        </el-text>
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="dialogVisible = false">取 消</el-button>
      <el-button type="primary" @click="handleSubmit">确 定</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbAccountApi, FbAccountUpdateProxyReqVO } from '@/api/facebook/account'
import { SysProxyApi, SysProxyRespVO } from '@/api/system/proxy'
import { useMessage } from '@/hooks/web/useMessage'

const emit = defineEmits(['success'])
const message = useMessage()

const dialogVisible = ref(false)
const dialogTitle = ref('批量修改代理')
const selectedCount = ref(0)
const selectedIds = ref<number[]>([])

const formData = reactive({
  proxyId: null as number | null,
})

const proxyList = ref<SysProxyRespVO[]>([])

const open = (ids: number[]) => {
  dialogVisible.value = true
  selectedIds.value = ids
  selectedCount.value = ids.length
  formData.proxyId = null
}
defineExpose({ open })

const loadProxies = async () => {
  try {
    const data = await SysProxyApi.getAllEnabledProxyList()
    proxyList.value = data || []
  } catch (error) {
    console.error('加载代理失败:', error)
  }
}

const handleSubmit = async () => {
  try {
    const data: FbAccountUpdateProxyReqVO = {
      ids: selectedIds.value,
      proxyId: formData.proxyId,
    }
    await FbAccountApi.updateFbAccountProxy(data)
    message.success('修改成功')
    dialogVisible.value = false
    emit('success')
  } catch (error) {
    message.error('修改失败')
  }
}

onMounted(() => {
  loadProxies()
})
</script>
<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="500px">
    <el-form :model="formData" label-width="100px">
      <el-form-item label="选择分组">
        <el-select v-model="formData.groupId" placeholder="请选择分组" class="w-full">
          <el-option :value="null" label="不分组" />
          <el-option
            v-for="group in groupList"
            :key="group.id"
            :value="group.id"
            :label="group.groupName"
          />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-text type="info" size="small">将为选中的 {{ selectedCount }} 个账号设置分组</el-text>
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="dialogVisible = false">取消</el-button>
      <el-button type="primary" @click="handleSubmit">确定</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { Dialog } from '@/components/Dialog'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { FbAccountApi, FbAccountUpdateGroupReqVO } from '@/api/facebook/account'
import { useMessage } from '@/hooks/web/useMessage'

const emit = defineEmits(['success'])
const message = useMessage()
const dialogVisible = ref(false)
const dialogTitle = ref('批量修改分组')
const selectedCount = ref(0)
const selectedIds = ref<Array<number | string>>([])
const groupList = ref<any[]>([])
const formData = reactive<{ groupId: number | string | null }>({ groupId: null })

const open = (ids: Array<number | string>) => {
  dialogVisible.value = true
  selectedIds.value = ids
  selectedCount.value = ids.length
  formData.groupId = null
}
defineExpose({ open })

const handleSubmit = async () => {
  try {
    const data: FbAccountUpdateGroupReqVO = {
      ids: selectedIds.value,
      groupId: formData.groupId
    }
    await FbAccountApi.updateFbAccountGroup(data)
    message.success('修改成功')
    dialogVisible.value = false
    emit('success')
  } catch (error) {
    message.error('修改失败')
  }
}

onMounted(async () => {
  try {
    groupList.value = (await AccountGroupApi.getAllEnabledGroups()) || []
  } catch (error) {
    console.error('加载分组失败:', error)
  }
})
</script>

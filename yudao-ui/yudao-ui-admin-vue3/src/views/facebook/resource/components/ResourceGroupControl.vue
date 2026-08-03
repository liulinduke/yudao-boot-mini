<template>
  <div class="resource-group-control">
    <el-select v-model="model" clearable placeholder="全部分组" class="group-select" @change="emit('change')">
      <el-option v-for="item in groups" :key="item.id" :label="item.name" :value="item.id" />
    </el-select>
    <el-button link type="primary" title="管理分组" @click="openManage">
      <Icon icon="ep:setting" />
    </el-button>

    <el-dialog v-model="manageVisible" :title="`${title}管理`" width="420px" append-to-body>
      <div class="group-manage-row" v-for="item in customGroups" :key="item.id">
        <span>{{ item.name }}</span>
        <span>
          <el-button link type="primary" @click="rename(item)">重命名</el-button>
          <el-button link type="danger" @click="remove(item)">删除</el-button>
        </span>
      </div>
      <el-empty v-if="customGroups.length === 0" description="暂无自定义分组" :image-size="60" />
      <template #footer>
        <el-button type="primary" @click="add">新增分组</el-button>
        <el-button @click="manageVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessageBox } from 'element-plus'
import { useMessage } from '@/hooks/web/useMessage'
import { FbResourceGroupApi, type FbResourceGroup, type FbResourceType } from '@/api/facebook/resourcegroup'

const props = defineProps<{ modelValue?: number; resourceType: FbResourceType; title: string }>()
const emit = defineEmits<{ 'update:modelValue': [value?: number]; change: [] }>()
const message = useMessage()
const groups = ref<FbResourceGroup[]>([])
const manageVisible = ref(false)
const model = computed({
  get: () => props.modelValue,
  set: (value?: number) => emit('update:modelValue', value)
})
const customGroups = computed(() => groups.value.filter((item) => !item.isDefault))

const load = async () => {
  groups.value = (await FbResourceGroupApi.getList(props.resourceType)) || []
}
const openManage = async () => {
  await load()
  manageVisible.value = true
}
const add = async () => {
  const result = await ElMessageBox.prompt('请输入分组名称', '新增分组', { inputPattern: /\S+/, inputErrorMessage: '分组名称不能为空' })
  await FbResourceGroupApi.create({ name: result.value.trim(), resourceType: props.resourceType })
  await load()
}
const rename = async (item: FbResourceGroup) => {
  const result = await ElMessageBox.prompt('请输入新的分组名称', '重命名分组', { inputValue: item.name, inputPattern: /\S+/, inputErrorMessage: '分组名称不能为空' })
  await FbResourceGroupApi.update({ id: item.id, name: result.value.trim(), resourceType: props.resourceType })
  await load()
}
const remove = async (item: FbResourceGroup) => {
  await message.delConfirm()
  await FbResourceGroupApi.delete(item.id)
  if (model.value === item.id) model.value = undefined
  await load()
}
watch(() => props.resourceType, load)
onMounted(load)
</script>

<style scoped>
.resource-group-control { display: inline-flex; align-items: center; gap: 2px; }
.group-select { width: 150px; }
.group-manage-row { display: flex; align-items: center; justify-content: space-between; padding: 9px 0; border-bottom: 1px solid var(--el-border-color-lighter); }
</style>

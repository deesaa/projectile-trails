1) Так как сетка все время будет пересчитываться вместе с движением камеры, перенес вычисление финальных позиций вершин в шейдер.
2) Шаблон сетки (все вершины, треугольники и прочаяя информация) создается только один раз - при появлении нового проджектайла. Дальше все вычисления идут только на шейдере.
3) Количество нужных сегментов сетки для описания парраболы не равномерно - есть много прямых участков, где не нужна сильная сегментация. Добавил параметр `minBetweenSegmentAngle`, определяющий в какой момент надо создавать новый участой сетки в зависимости от угла между направлениеми проджектайла в соседних точках парраболы.
4) Добавил пул сеток, чтобы не создавать их заного при новом запросе на сетку из "20 сегментов", если такая сетка была уже создана для предыдущего проджектайла, который уже умер.
5) Отвязал рендер сетки от гейм обжектов, все сетки отправляются на отрисовку через  Graphics.RenderMesh
6) Для всех сеток используется один материал, так что динамический батчинг объединяет все в один вызов отрисовки.

### Что еще можно сделать?

1) На создании проджектайла происходит просчет всех его будущих позиций - однотиплая логика, котокую можно запихать в джобы.
2) Динамический батчинг может быть не самым оптимальным при объединении кучи мальних сеток. Можно сделать свои чанки меша и объединять сетки вручную при создании новых проджекталов.
3) Точность сетки на большом растоянии от камеры не так важна, как в близи. Можно повторять парраболу в отдалении менее точно. 
4) Можно попробовать даже просчет точек парраболы перенести в шейдер. Чтобы вообще все вычиления были на шейдере. Но это вычиления с одинаковыми результами для каждой вершины, так что скорее всего выйдет только путая нагрузка

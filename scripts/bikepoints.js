var bikepointsSC = (function () {
    var map;
    var dblClkMarker;
    var markers = [];
    var painting = false;
    var initMapfunc = function(){
        var myOptions = {
            zoom: 10, disableDoubleClickZoom: true,
            center: new google.maps.LatLng(51.5073509, -0.12775829999998223),
            mapTypeId: google.maps.MapTypeId.ROADMAP
        };
        map = new google.maps.Map($('#gmap_canvas')[0], myOptions);
        $("#gmap_canvas").height(($(window).height() - $("#up_part").height()) * 0.9);
        $("#foundedBikePointsTable").tablesorter();
        //add listener for dblclick
        addDblClkListerFunc();
        //init change top points event
        $("#TopPointsInput").change(refreshTopPoints);
        //init clear all markers button
        $("#ClearMarkersButton").click(clearMarkers);
        //init show all markers button
        $("#GetAllMarkers").click(ajaxRequestAllPoints);//getJSonString);
        refreshTopPoints();
        //set wait gif
        $body = $("body");

        $(document).on({
            ajaxStart: function () { $body.addClass("loading"); },
            ajaxStop: function () { $body.removeClass("loading"); }
        });
        
    }

    var addDblClkListerFunc = function () {
        dblClkMarker = new google.maps.Marker({ map: map });
        map.addListener("dblclick", function (event) {
            //dblClkMarker.setPosition(event.latLng);
            showPointsInRadius(event, dblClkMarker);
        });
    }

    var setMapOnAll = function (work_map) {
        for (var i = 0; i < markers.length; i++) {
            markers[i].setMap(work_map);
        }
    }

    var clearMarkers = function () {
        setMapOnAll(null);
    }

    var deleteMarkers = function () {
        clearMarkers();
        markers = [];
    }

    var ajaxRequestAllPoints = function () {
        clearMarkers();
        $.ajax({
            type: 'GET',
            url: '/LondonBikePointFinderService.asmx/GetAllBikeStops',
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            success: function onSuccessJSONGet(response, status) {
                PaintMarkers($.parseJSON(response.d.Result));
            },
            error: function (data) {
                console.write(data);
            }
        });
    }

    var initTableDivFunc = function () {
        $('#top_table_div').html("Working");
    }

    var PaintMarkers = function (data) {
        var Bounds = new google.maps.LatLngBounds();
        var tableData = "<table id=\"foundedBikePointsTable\" class=\"tablesorter\"><thead><tr><th>BikePoint Name</th>" 
            +"<th>Free bikes</th><th>Free docks</th></tr></thead><tbody>";
        $.each(data, function () {
            //set marker color
            //greener - have free bikes
            //red - no bikes available
            if (this.additionalProperties[1].value == "true" &&
                this.additionalProperties[2].value == "false") {
                var part = Math.floor((this.additionalProperties[6].value / this.additionalProperties[8].value) * 255);
                var pinColor
                if (part >= 0 && part < 16) {
                    pinColor = "0" + part.toString(16).toUpperCase() +
                        (255 - part).toString(16).toUpperCase() + "00";
                }
                else if (part <= 255 && part > 239) {
                    pinColor = "" + (part).toString(16).toUpperCase() + "0"
                        + (255 - part).toString(16).toUpperCase() + "00";
                }
                else if (part >= 16 && part <= 239) {
                    pinColor = "" + (part).toString(16).toUpperCase()
                        + (255 - part).toString(16).toUpperCase() + "00";
                }
                var pinImage = new google.maps.MarkerImage("http://chart.apis.google.com/chart?chst=d_map_pin_letter&chld=%E2%80%A2|"
                    + pinColor, new google.maps.Size(21, 34), new google.maps.Point(0, 0), new google.maps.Point(10, 34));
                var marker = new google.maps.Marker({ map: map, icon: pinImage });
                marker.setPosition({ lat: this.lat, lng: this.lon });
                Bounds.extend({ lat: this.lat, lng: this.lon });
                var infoString = "At " + this.commonName + " " + this.additionalProperties[7].value
                    + " bikes availible";
                var infowindow = new google.maps.InfoWindow({
                    content: infoString
                });
                marker.addListener('click', function () {
                    infowindow.open(map, marker);
                });
                markers.push(marker);
                tableData += "<tr>";
                tableData += "<td>";
                tableData += this.commonName;
                tableData += "</td>";
                tableData += "<td>";
                tableData += this.additionalProperties[6].value;
                tableData += "</td>";
                tableData += "<td>";
                tableData += this.additionalProperties[7].value;
                tableData += "</td>";
                tableData += "</tr>";
            }
            else {
                console.log(this.commonName + " locked or not onstalled");
            }
        });
        if (Bounds.isEmpty() === false) {
            Bounds.extend(dblClkMarker.getPosition());
            map.setCenter(Bounds.getCenter(), map.fitBounds(Bounds));
            tableData += "</tbody></table>"
            $("#foundedBikePointsTableDiv").html(tableData);
            $("#foundedBikePointsTable").tablesorter();
        }
    }

    var refreshTopPoints = function () {
        var topPointsToShow = $("#TopPointsInput")[0].value;
        if (topPointsToShow > 0) {
            $.ajax({
                type: 'POST',
                url: '/LondonBikePointFinderService.asmx/RefreshTopPoints',
                data: JSON.stringify({ topValue: topPointsToShow }),
                contentType: 'application/json; charset=utf-8',
                dataType: 'json',
                success: function onSuccessRefreshGet(response, status) {
                    $('#top_table_div').html(response.d);
                }
            });
        }
        else
        {
            console.log("Check input top points value");
        }
    }

    var showPointsInRadius = function (event, marker) {
        var radius_value = $("#RadiusInput")[0].value;
        if (painting === false && radius_value > 1) {
            //check radius            
            painting = true;
            console.log("Painting if: " + painting);
            marker.setPosition(event.latLng);
            clearMarkers();
            $.ajax({
                type: 'POST',
                url: '/LondonBikePointFinderService.asmx/GetPointsWithRadius',
                data: JSON.stringify({ lat: marker.getPosition().lat(), lon: marker.getPosition().lng(), radius: radius_value }),
                contentType: 'application/json; charset=utf-8',
                dataType: 'json',
                success: function onSuccessRefreshGet(response, status) {
                    var objs = $.parseJSON(response.d.Result);
                    if (objs.length > 0) {
                        PaintMarkers(objs);
                        refreshTopPoints();
                    }
                    else {
                        alert("Nothing was found");
                    }
                    painting = false;
                },
                error: function(){
                    painting = false;
                }
            });
        }
        else {
            console.log("I'm not painting. Or i'm already painting, or check radius input");
        };

    }

    var getJSonString = function () {
        var url_get = "https://api.tfl.gov.uk/BikePoint?app_id=57e5756c&app_key=fa0bd9e6338c902e6ef998ff24fd5607"
        $.getJSON(url_get, function (data) {
            PaintMarkers(data);
        });
    }

    return {
        initMap: initMapfunc,
        initTableDiv: initTableDivFunc
    }
}
)();
$(document).ready(bikepointsSC.initMap);